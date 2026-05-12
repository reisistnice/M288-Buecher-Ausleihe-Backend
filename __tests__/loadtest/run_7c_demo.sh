#!/usr/bin/env bash
# 7c — Remove protection, show failure, restore, show pass
#
# Usage: ./run_7c_demo.sh
# Prerequisites: backend running at http://localhost:5068 AND npm install done
#
# What this script does:
#   1. Patches LoanRepository.cs to replace BorrowAsync with the unprotected version
#      (no SERIALIZABLE transaction, with Task.Delay(100) to widen the race window)
#   2. Rebuilds the backend
#   3. Runs npm test — the concurrent borrow test MUST FAIL
#   4. Saves output to without_protection_output.txt
#   5. Restores the original LoanRepository.cs
#   6. Rebuilds
#   7. Runs npm test — all tests MUST PASS

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
LOAN_REPO="$REPO_ROOT/Backend/Infrastructure/Repositories/LoanRepository.cs"
BACKUP="$LOAN_REPO.bak"
OUTPUT="$SCRIPT_DIR/without_protection_output.txt"
API_PROJECT="$REPO_ROOT/Backend/Api"

echo "=== 7c Demo: Removing SERIALIZABLE protection ==="

# ── Step 1: backup original ───────────────────────────────────────────────────
cp "$LOAN_REPO" "$BACKUP"
echo "[1/7] Backed up LoanRepository.cs"

# ── Step 2: inject unprotected BorrowAsync ───────────────────────────────────
# Replace the entire BorrowAsync method with the unprotected version that has
# Task.Delay(100) between the availability check and the insert — this widens
# the race window so the test fails reliably.
python3 - <<'PYEOF'
import re, sys

path = sys.argv[1]
with open(path) as f:
    src = f.read()

unprotected = '''    // 7c WITHOUT_TRANSACTION — injected by run_7c_demo.sh for demo purposes
    public async Task<(Loan? Loan, string? Error)> BorrowAsync(int bookId, int userId)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book is null) return (null, "BOOK_NOT_FOUND");

        var activeLoans = await _context.Loans
            .CountAsync(l => l.BookId == bookId && l.ReturnDate == null);

        // Task.Delay widens the race window so concurrent requests both pass the check
        await Task.Delay(100);

        if (activeLoans >= book.TotalCopies) return (null, "NO_COPIES_AVAILABLE");

        var newLoan = new Loan { BookId = bookId, UserId = userId };
        _context.Loans.Add(newLoan);
        await _context.SaveChangesAsync();

        var full = await _context.Loans
            .Include(l => l.Book).Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == newLoan.Id);
        return (full, null);
    }'''

# Replace from "public async Task<(Loan?" through the closing brace of the method
src = re.sub(
    r'    // ── 7c WITHOUT_TRANSACTION.*?^    \}',
    unprotected,
    src, flags=re.DOTALL | re.MULTILINE
)
with open(path, 'w') as f:
    f.write(src)
print("Patched LoanRepository.cs with unprotected BorrowAsync")
PYEOF
python3 - "$LOAN_REPO"
echo "[2/7] Patched LoanRepository.cs (no transaction, Task.Delay injected)"

# ── Step 3: rebuild backend ───────────────────────────────────────────────────
echo "[3/7] Rebuilding backend..."
dotnet build "$API_PROJECT" -c Release --nologo -v q
echo "      Rebuild done. Restart the backend now, then press ENTER to continue."
read -r

# ── Step 4: run tests, expect failure ─────────────────────────────────────────
echo "[4/7] Running concurrent borrow test WITHOUT protection (expect FAIL)..."
set +e
cd "$REPO_ROOT"
npm test -- --testNamePattern="two users borrow last copy" 2>&1 | tee "$OUTPUT"
TEST_EXIT=$?
set -e
echo ""
echo "[4/7] Test exit code: $TEST_EXIT"

if [ $TEST_EXIT -eq 0 ]; then
    echo "WARNING: test passed unexpectedly — race may not have triggered. Try rerunning."
else
    echo "✓ Test failed as expected — race condition confirmed."
fi
echo "[4/7] Failure output saved to: $OUTPUT"

# ── Step 5: restore original ──────────────────────────────────────────────────
cp "$BACKUP" "$LOAN_REPO"
rm "$BACKUP"
echo "[5/7] Restored original LoanRepository.cs (SERIALIZABLE + retry)"

# ── Step 6: rebuild ───────────────────────────────────────────────────────────
echo "[6/7] Rebuilding backend with protection restored..."
dotnet build "$API_PROJECT" -c Release --nologo -v q
echo "      Rebuild done. Restart the backend now, then press ENTER to continue."
read -r

# ── Step 7: run tests, expect all pass ────────────────────────────────────────
echo "[7/7] Running all tests WITH protection (expect PASS)..."
npm test
echo ""
echo "=== 7c Demo complete ==="
echo "Failing output: $OUTPUT"
