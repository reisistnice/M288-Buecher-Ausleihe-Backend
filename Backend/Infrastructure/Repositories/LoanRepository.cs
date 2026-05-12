using Application.Common.Interfaces;
using Core.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class LoanRepository : ILoanRepository
{
    private readonly AppDbContext _context;

    public LoanRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Loan>> GetAllAsync() =>
        await _context.Loans.Include(l => l.Book).Include(l => l.User).ToListAsync();

    public async Task<List<Loan>> GetByUserIdAsync(int userId) =>
        await _context.Loans.Include(l => l.Book).Include(l => l.User)
            .Where(l => l.UserId == userId && l.ReturnDate == null).ToListAsync();

    public async Task<Loan?> GetByIdAsync(int id) =>
        await _context.Loans.Include(l => l.Book).Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == id);

    public async Task<Loan> CreateAsync(Loan loan)
    {
        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();
        return loan;
    }

    // ---- 7c WITHOUT_TRANSACTION (race condition demo) -------
    // Without SERIALIZABLE isolation, two concurrent BorrowAsync calls both read
    // activeLoans = 0, both pass the check (0 < 1 = TotalCopies), and both insert
    // a Loan row. Result: 2 active loans for a book with TotalCopies = 1.
    //
    // Unprotected code:
    //
    //   var book = await _context.Books.FindAsync(bookId);
    //   if (book is null) return (null, "BOOK_NOT_FOUND");
    //   // Task.Delay(100) widens the race window for test demos
    //   await Task.Delay(100);
    //   var activeLoans = await _context.Loans
    //       .CountAsync(l => l.BookId == bookId && l.ReturnDate == null);
    //   if (activeLoans >= book.TotalCopies) return (null, "NO_COPIES_AVAILABLE");
    //   var newLoan = new Loan { BookId = bookId, UserId = userId };
    //   _context.Loans.Add(newLoan);
    //   await _context.SaveChangesAsync();
    //   return (newLoan, null);
    //
    // With 10 concurrent requests and 1 copy, ALL requests can succeed —
    // active_loans goes to 10. The unit test catches this.
    // ----- END WITHOUT_TRANSACTION --------------
    public async Task<(Loan? Loan, string? Error)> BorrowAsync(int bookId, int userId)
    {
        const int maxRetries = 3;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);
            try
            {
                var book = await _context.Books.FindAsync(bookId);
                if (book is null)
                {
                    await transaction.RollbackAsync();
                    return (null, "BOOK_NOT_FOUND");
                }

                var activeLoans = await _context.Loans
                    .CountAsync(l => l.BookId == bookId && l.ReturnDate == null);

                if (activeLoans >= book.TotalCopies)
                {
                    await transaction.RollbackAsync();
                    return (null, "NO_COPIES_AVAILABLE");
                }

                var newLoan = new Loan { BookId = bookId, UserId = userId };
                _context.Loans.Add(newLoan);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var full = await _context.Loans
                    .Include(l => l.Book).Include(l => l.User)
                    .FirstOrDefaultAsync(l => l.Id == newLoan.Id);
                return (full, null);
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* already rolled back by SQL Server */ }

                if (!IsSerializationConflict(ex) || attempt == maxRetries - 1)
                {
                    // Return 503 on exhausted retries; rethrow truly unexpected errors
                    if (IsSerializationConflict(ex))
                        return (null, "SERIALIZATION_CONFLICT");
                    throw;
                }

                await Task.Delay(20 * (attempt + 1));
            }
        }

        return (null, "SERIALIZATION_CONFLICT");
    }

    // Atomic conditional UPDATE — prevents double-return without needing a separate transaction.
    // Two concurrent callers both execute:
    //   UPDATE Loans SET ReturnDate = @now WHERE Id = @id AND ReturnDate IS NULL
    // SQL Server serialises X-lock acquisition: exactly one gets 1 row affected,
    // the other gets 0 → returns null → controller returns 404.
    public async Task<Loan?> ReturnAsync(int loanId)
    {
        var now = DateTime.UtcNow;
        var affected = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Loans SET ReturnDate = {now} WHERE Id = {loanId} AND ReturnDate IS NULL");

        if (affected == 0) return null;

        return await _context.Loans
            .Include(l => l.Book).Include(l => l.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == loanId);
    }

    // Recursively searches inner exceptions for SQL Server serialization/deadlock errors.
    private static bool IsSerializationConflict(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is SqlException sqlEx && sqlEx.Number is 1205 or 3960)
                return true;
            var msg = ex.Message.ToLowerInvariant();
            if (msg.Contains("deadlock") || msg.Contains("serializ") || msg.Contains("snapshot"))
                return true;
            ex = ex.InnerException;
        }
        return false;
    }
}
