using Application.Common.Interfaces;
using Core.Entities;
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
            .Where(l => l.UserId == userId).ToListAsync();

    public async Task<Loan?> GetByIdAsync(int id) =>
        await _context.Loans.Include(l => l.Book).Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == id);

    public async Task<Loan> CreateAsync(Loan loan)
    {
        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();
        return loan;
    }

    public async Task<Loan?> ReturnAsync(int loanId)
    {
        var loan = await _context.Loans.Include(l => l.Book).FirstOrDefaultAsync(l => l.Id == loanId);
        if (loan is null || loan.ReturnDate.HasValue) return null;

        loan.ReturnDate = DateTime.UtcNow;
        loan.Book.AvailableCopies++;
        await _context.SaveChangesAsync();
        return loan;
    }
}
