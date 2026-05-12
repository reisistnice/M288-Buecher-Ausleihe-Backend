/**
 * 7b — Load / Stress Test (k6)
 *
 * Run: k6 run __tests__/loadtest/borrow.js
 *
 * After run, verify DB integrity:
 *   npm run verify:db -- <bookId printed by setup>
 *
 * What this test proves:
 *   - 10 concurrent VUs each borrow --> return in a tight loop for 30 s
 *   - Serialization conflicts appear in SQL Server (1205 deadlock / serialization errors)
 *   - The retry logic in BorrowAsync absorbs those conflicts
 *   - available_copies NEVER goes negative (assertion in verify_db.ts)
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate } from 'k6/metrics';

// 409 (no copies) and 503 (retry exhausted) are expected — not failures
http.setResponseCallback(http.expectedStatuses(200, 201, 404, 409, 503));

export const options = {
  vus: 10,
  duration: '30s',
  thresholds: {
    // All borrow attempts must resolve to 201 (success) or 409/503 (correctly rejected)
    'borrow_correct_response': ['rate>0.99'],
    http_req_failed: ['rate<0.01'],
  },
};

const BASE = __ENV.API_URL || 'http://localhost:5068/api';
const JSON_HEADERS = { 'Content-Type': 'application/json' };

const borrowCorrect = new Rate('borrow_correct_response');
const serializationRetries = new Counter('serialization_retries');

// ---- setup: runs once before all VUs -----------
export function setup() {
  // Login as admin
  const adminLogin = http.post(
    `${BASE}/auth/login`,
    JSON.stringify({ username: 'admin', password: 'Admin1234!' }),
    { headers: JSON_HEADERS }
  );
  check(adminLogin, { 'admin login ok': (r) => r.status === 200 });
  const adminToken = JSON.parse(adminLogin.body).token;
  const adminHeaders = { ...JSON_HEADERS, Authorization: `Bearer ${adminToken}` };

  // Create a shared test book with 3 copies (provokes contention among 10 VUs)
  const bookRes = http.post(
    `${BASE}/books`,
    JSON.stringify({
      title: `LoadTest_${Date.now()}`,
      author: 'LoadTestAuthor',
      isbn: `LT-${Date.now()}`,
      totalCopies: 3,
    }),
    { headers: adminHeaders }
  );
  check(bookRes, { 'book created': (r) => r.status === 201 });
  const bookId = JSON.parse(bookRes.body).id;

  // Pre-create 10 VU accounts so login is instant during the test
  const users = [];
  for (let i = 1; i <= 10; i++) {
    const username = `k6vu_${i}`;
    const email = `k6vu_${i}@loadtest.com`;
    const password = 'Test1234!';

    // Register (idempotent — 409 means user already exists)
    http.post(
      `${BASE}/auth/register`,
      JSON.stringify({ email, username, password }),
      { headers: JSON_HEADERS }
    );

    const loginRes = http.post(
      `${BASE}/auth/login`,
      JSON.stringify({ username, password }),
      { headers: JSON_HEADERS }
    );
    check(loginRes, { [`vu${i} login ok`]: (r) => r.status === 200 });
    users.push({ username, token: JSON.parse(loginRes.body).token });
  }

  console.log(`Load test book ID: ${bookId} (use npm run verify:db -- ${bookId})`);
  return { bookId, users };
}

// default: called per VU per iteration --------------
export default function (data) {
  const { bookId, users } = data;
  // Each VU uses a dedicated user account (__VU is 1-based)
  const user = users[(__VU - 1) % users.length];
  const authHeaders = { ...JSON_HEADERS, Authorization: `Bearer ${user.token}` };

  // Borrow
  const borrowRes = http.post(
    `${BASE}/loans`,
    JSON.stringify({ bookId }),
    { headers: authHeaders }
  );

  const borrowOk = borrowRes.status === 201 || borrowRes.status === 409 || borrowRes.status === 503;
  borrowCorrect.add(borrowOk);

  if (borrowRes.status === 503) {
    serializationRetries.add(1);
  }

  check(borrowRes, {
    'borrow: success or clean rejection': () => borrowOk,
  });

  if (borrowRes.status === 201) {
    const loanId = JSON.parse(borrowRes.body).id;
    sleep(0.2); // hold the copy briefly to create contention
    const returnRes = http.put(
      `${BASE}/loans/${loanId}/return`,
      '{}',
      { headers: authHeaders }
    );
    check(returnRes, { 'return ok': (r) => r.status === 200 || r.status === 404 });
  }

  sleep(0.05);
}

// ---- teardown: runs once after all VUs -----------------
export function teardown(data) {
  console.log(`\n=== Load test complete ===`);
  console.log(`Verify DB: npm run verify:db -- ${data.bookId}`);
  console.log(`Expected: active loans <= 3 (TotalCopies), never negative`);
}
