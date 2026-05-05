using Core.Entities;

namespace Application.Common.Interfaces;

public interface ILoanRepository
{
    Task<List<Loan>> GetAllAsync();
    Task<List<Loan>> GetByUserIdAsync(int userId);
    Task<Loan?> GetByIdAsync(int id);
    Task<Loan> CreateAsync(Loan loan);
    Task<Loan?> ReturnAsync(int loanId);
}
