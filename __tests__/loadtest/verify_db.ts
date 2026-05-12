/**
 * 7b — Post-load-test DB integrity verifier
 *
 * Usage: npm run verify:db -- <bookId>
 *
 * Connects to SQL Server and asserts:
 *   1. active loans never exceeded TotalCopies (no negative availability)
 *   2. every Loan row has a valid BookId and UserId
 *   3. no loan has ReturnDate < LoanDate
 */

import * as sql from 'mssql';

const DB_CONFIG: sql.config = {
  server: 'localhost',
  port: 1433,
  database: 'BuecherAusleiheDb',
  user: 'sa',
  password: 'SicheresPasswort123!',
  options: { encrypt: false, trustServerCertificate: true, enableArithAbort: true },
};

async function main() {
  const bookId = parseInt(process.argv[2] ?? '0', 10);
  if (!bookId) {
    console.error('Usage: npm run verify:db -- <bookId>');
    process.exit(1);
  }

  const pool = await sql.connect(DB_CONFIG);
  let passed = true;

  // 1. active loans vs TotalCopies
  const copiesRes = await pool
    .request()
    .input('b', sql.Int, bookId)
    .query('SELECT TotalCopies FROM Books WHERE Id = @b');

  if (!copiesRes.recordset.length) {
    console.error(`✗ Book ${bookId} not found in DB`);
    await pool.close();
    process.exit(1);
  }
  const totalCopies: number = copiesRes.recordset[0].TotalCopies;

  const activeRes = await pool
    .request()
    .input('b', sql.Int, bookId)
    .query('SELECT COUNT(*) AS cnt FROM Loans WHERE BookId = @b AND ReturnDate IS NULL');
  const activeLoans: number = activeRes.recordset[0].cnt;

  const available = totalCopies - activeLoans;

  console.log(`Book ${bookId}: TotalCopies=${totalCopies}, ActiveLoans=${activeLoans}, Available=${available}`);

  if (available < 0) {
    console.error(`✗ FAIL: available_copies went negative! (${available})`);
    passed = false;
  } else {
    console.log(`✓ available_copies >= 0 (${available})`);
  }

  // 2. all loans have valid book / user references
  const orphanRes = await pool
    .request()
    .input('b', sql.Int, bookId)
    .query(`
      SELECT COUNT(*) AS cnt
      FROM Loans l
      WHERE l.BookId = @b
        AND (NOT EXISTS (SELECT 1 FROM Books bk WHERE bk.Id = l.BookId)
          OR NOT EXISTS (SELECT 1 FROM Users u WHERE u.Id = l.UserId))
    `);
  const orphans: number = orphanRes.recordset[0].cnt;
  if (orphans > 0) {
    console.error(`✗ FAIL: ${orphans} orphaned loan(s) found`);
    passed = false;
  } else {
    console.log(`✓ No orphaned loans`);
  }

  // 3. return date must be after loan date
  const invalidDatesRes = await pool
    .request()
    .input('b', sql.Int, bookId)
    .query(`
      SELECT COUNT(*) AS cnt
      FROM Loans
      WHERE BookId = @b AND ReturnDate IS NOT NULL AND ReturnDate < LoanDate
    `);
  const badDates: number = invalidDatesRes.recordset[0].cnt;
  if (badDates > 0) {
    console.error(`✗ FAIL: ${badDates} loan(s) with ReturnDate < LoanDate`);
    passed = false;
  } else {
    console.log(`✓ All return dates valid`);
  }

  // summary
  const allLoansRes = await pool
    .request()
    .input('b', sql.Int, bookId)
    .query(`
      SELECT l.Id, l.UserId, l.LoanDate, l.ReturnDate
      FROM Loans l WHERE l.BookId = @b
      ORDER BY l.LoanDate
    `);
  console.log(`\nAll loans for book ${bookId} (${allLoansRes.recordset.length} total):`);
  allLoansRes.recordset.forEach((row: any) =>
    console.log(`  Loan#${row.Id}  user=${row.UserId}  returned=${row.ReturnDate ? 'yes' : 'no'}`)
  );

  await pool.close();

  if (!passed) {
    console.error('\n=== VERIFICATION FAILED ===');
    process.exit(1);
  }
  console.log('\n=== All assertions passed ===');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
