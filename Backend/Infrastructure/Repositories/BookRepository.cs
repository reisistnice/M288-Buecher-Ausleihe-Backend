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

    public async Task<List<Book>> GetAllAsync()
    {
        var books = await _context.Books.Include(b => b.Loans).ToListAsync();
        foreach (var b in books)
            b.AvailableCopies = b.TotalCopies - b.Loans.Count(l => l.ReturnDate == null);
        return books;
    }

    public async Task<Book?> GetByIdAsync(int id)
    {
        var book = await _context.Books.Include(b => b.Loans).FirstOrDefaultAsync(b => b.Id == id);
        if (book is not null)
            book.AvailableCopies = book.TotalCopies - book.Loans.Count(l => l.ReturnDate == null);
        return book;
    }

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
