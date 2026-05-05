using Core.Entities;

namespace Application.Common.Interfaces;

public interface IBookRepository
{
    Task<List<Book>> GetAllAsync();
    Task<Book?> GetByIdAsync(int id);
    Task<Book> CreateAsync(Book book);
    Task<Book?> UpdateAsync(Book book);
    Task<bool> DeleteAsync(int id);
}
