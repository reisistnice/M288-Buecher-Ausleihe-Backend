using Application.Common.Interfaces;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class BookRepository : IBookRepository
{
    private readonly AppDbContext _context;

    public BookRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Book>> GetAllAsync() =>
        await _context.Books.ToListAsync();

    public async Task<Book?> GetByIdAsync(int id) =>
        await _context.Books.FindAsync(id);

    public async Task<Book> CreateAsync(Book book)
    {
        _context.Books.Add(book);
        await _context.SaveChangesAsync();
        return book;
    }

    public async Task<Book?> UpdateAsync(Book book)
    {
        _context.Books.Update(book);
        await _context.SaveChangesAsync();
        return book;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var book = await _context.Books.FindAsync(id);
        if (book is null) return false;
        _context.Books.Remove(book);
        await _context.SaveChangesAsync();
        return true;
    }
}
