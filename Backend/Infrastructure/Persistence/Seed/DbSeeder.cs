using Application.Common.Interfaces;
using Core.Entities;

namespace Infrastructure.Persistence.Seed;

public class DbSeeder : IDbSeeder
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public DbSeeder(AppDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync()
    {
        if (!_context.Users.Any())
        {
            _context.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = _passwordHasher.Hash("Admin1234!"),
                Role = UserRole.Administrators
            });
            _context.Users.Add(new User
            {
                Username = "user",
                PasswordHash = _passwordHasher.Hash("User1234!"),
                Role = UserRole.Users
            });
            await _context.SaveChangesAsync();
        }

        if (!_context.Books.Any())
        {
            _context.Books.AddRange(
                new Book { Title = "Clean Code", Author = "Robert C. Martin", ISBN = "978-0132350884" },
                new Book { Title = "The Pragmatic Programmer", Author = "David Thomas", ISBN = "978-0135957059" },
                new Book { Title = "Design Patterns", Author = "Gang of Four", ISBN = "978-0201633610" }
            );
            await _context.SaveChangesAsync();
        }
    }
}
