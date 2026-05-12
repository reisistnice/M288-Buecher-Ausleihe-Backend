/**
 * 7a — Concurrent Unit Tests
 *
 * Prerequisites: backend running at http://localhost:5068
 *   cd Backend/Api && dotnet run
 *
 * Tests use real DB (no mocks) — as required by slide set PR_223_1015_Tests.pdf.
 * Race conditions are provoked by firing N concurrent HTTP requests via Promise.all().
 */

import axios, { AxiosError } from 'axios';
import * as sql from 'mssql';

const API = process.env.API_URL || 'http://localhost:5068/api';

const DB_CONFIG: sql.config = {
  server: 'localhost',
  port: 1433,
  database: 'BuecherAusleiheDb',
  user: 'sa',
  password: 'SicheresPasswort123!',
  options: { encrypt: false, trustServerCertificate: true, enableArithAbort: true },
};

// ── helpers ───────────────────────────────────────────────────────────────────

let pool: sql.ConnectionPool;
let adminToken: string;
const tokens: string[] = [];

async function login(username: string, password: string): Promise<string> {
  const res = await axios.post(`${API}/auth/login`, { username, password });
  return res.data.token;
}

async function ensureUser(email: string, username: string, password: string): Promise<void> {
  try {
    await axios.post(`${API}/auth/register`, { email, username, password });
  } catch (e: any) {
    if ((e as AxiosError).response?.status !== 409) throw e;
  }
}

async function borrowBook(
  bookId: number,
  token: string
): Promise<{ success: boolean; loanId?: number; status: number }> {
  try {
    const res = await axios.post(
      `${API}/loans`,
      { bookId },
      { headers: { Authorization: `Bearer ${token}` } }
    );
    return { success: true, loanId: res.data.id, status: res.status };
  } catch (e: any) {
    return { success: false, status: (e as AxiosError).response?.status ?? 0 };
  }
}

async function returnLoan(loanId: number, token: string): Promise<{ success: boolean; status: number }> {
  try {
    await axios.put(
      `${API}/loans/${loanId}/return`,
      {},
      { headers: { Authorization: `Bearer ${token}` } }
    );
    return { success: true, status: 200 };
  } catch (e: any) {
    return { success: false, status: (e as AxiosError).response?.status ?? 0 };
  }
}

async function createTestBook(totalCopies: number): Promise<number> {
  const res = await axios.post(
    `${API}/books`,
    { title: `TestBook_${Date.now()}`, author: 'TestAuthor', isbn: `TST-${Date.now()}`, totalCopies },
    { headers: { Authorization: `Bearer ${adminToken}` } }
  );
  return res.data.id;
}

async function getActiveLoanCount(bookId: number): Promise<number> {
  const result = await pool
    .request()
    .input('bookId', sql.Int, bookId)
    .query('SELECT COUNT(*) AS cnt FROM Loans WHERE BookId = @bookId AND ReturnDate IS NULL');
  return result.recordset[0].cnt;
}

async function getTotalCopies(bookId: number): Promise<number> {
  const result = await pool
    .request()
    .input('bookId', sql.Int, bookId)
    .query('SELECT TotalCopies FROM Books WHERE Id = @bookId');
  return result.recordset[0].TotalCopies;
}

async function cleanupBook(bookId: number): Promise<void> {
  await pool.request().input('b', sql.Int, bookId).query('DELETE FROM Loans WHERE BookId = @b');
  await pool.request().input('b', sql.Int, bookId).query('DELETE FROM Books WHERE Id = @b');
}

// ── lifecycle ─────────────────────────────────────────────────────────────────

beforeAll(async () => {
  pool = await sql.connect(DB_CONFIG);
  adminToken = await login('admin', 'Admin1234!');

  // Create 10 test users for concurrent scenarios
  for (let i = 1; i <= 10; i++) {
    const ts = Date.now();
    const username = `concuser_${i}_${ts}`;
    const email = `concuser_${i}_${ts}@test.com`;
    await ensureUser(email, username, 'Test1234!');
    tokens.push(await login(username, 'Test1234!'));
  }
});

afterAll(async () => {
  await pool.close();
});

// ── test suite ────────────────────────────────────────────────────────────────

describe('Concurrent borrow', () => {
  let bookId: number;

  afterEach(async () => {
    if (bookId) await cleanupBook(bookId);
  });

  // ── 7a Test 1 ────────────────────────────────────────────────────────────
  test('two users borrow last copy simultaneously — exactly 1 succeeds', async () => {
    bookId = await createTestBook(1);

    // Fire 10 concurrent borrow requests to maximise race window probability
    const results = await Promise.all(tokens.slice(0, 10).map((t) => borrowBook(bookId, t)));

    const successes = results.filter((r) => r.success);
    const failures  = results.filter((r) => !r.success);

    // Exactly 1 loan created
    expect(successes).toHaveLength(1);
    expect(failures).toHaveLength(9);

    // All failures are 409 Conflict (NO_COPIES_AVAILABLE) or 503 (retry exhausted)
    failures.forEach((f) => expect([409, 503]).toContain(f.status));

    // DB must never show more active loans than total copies
    const activeLoans = await getActiveLoanCount(bookId);
    const totalCopies = await getTotalCopies(bookId);
    expect(activeLoans).toBeLessThanOrEqual(totalCopies);
    expect(activeLoans).toBe(1);
  });

  // ── 7a Test 2 ────────────────────────────────────────────────────────────
  test('return restores availability — second user can borrow after return', async () => {
    bookId = await createTestBook(1);

    // User 0 borrows
    const borrow1 = await borrowBook(bookId, tokens[0]);
    expect(borrow1.success).toBe(true);
    expect(await getActiveLoanCount(bookId)).toBe(1);

    // User 0 returns
    const ret = await returnLoan(borrow1.loanId!, tokens[0]);
    expect(ret.success).toBe(true);
    expect(await getActiveLoanCount(bookId)).toBe(0);

    // User 1 can now borrow
    const borrow2 = await borrowBook(bookId, tokens[1]);
    expect(borrow2.success).toBe(true);
    expect(await getActiveLoanCount(bookId)).toBe(1);
  });

  // ── 7a Test 3 ────────────────────────────────────────────────────────────
  test('concurrent returns of same loan — exactly 1 succeeds, no double-decrement', async () => {
    bookId = await createTestBook(1);

    // User 0 borrows
    const borrow = await borrowBook(bookId, tokens[0]);
    expect(borrow.success).toBe(true);
    const loanId = borrow.loanId!;
    expect(await getActiveLoanCount(bookId)).toBe(1);

    // Both users try to return the SAME loan simultaneously
    // (LoansController does not enforce ownership — any authenticated user may return)
    const [ret1, ret2] = await Promise.all([
      returnLoan(loanId, tokens[0]),
      returnLoan(loanId, tokens[1]),
    ]);

    const returnSuccesses = [ret1, ret2].filter((r) => r.success);
    const returnFailures  = [ret1, ret2].filter((r) => !r.success);

    expect(returnSuccesses).toHaveLength(1);
    expect(returnFailures).toHaveLength(1);
    // The failure must be 404 (already returned) — not a server error
    expect(returnFailures[0].status).toBe(404);

    // Available copies restored exactly once — active loans = 0
    expect(await getActiveLoanCount(bookId)).toBe(0);
  });
});
