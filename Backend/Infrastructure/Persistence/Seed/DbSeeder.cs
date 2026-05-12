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
        if (!_context.Users.Any(u => u.Username == "admin"))
        {
            _context.Users.Add(new User
            {
                Username = "admin",
                Email = "admin@buecherausleihe.local",
                PasswordHash = _passwordHasher.Hash("Admin1234!"),
                Role = UserRole.Administrators
            });
            await _context.SaveChangesAsync();
        }

        if (!_context.Users.Any(u => u.Username == "user"))
        {
            _context.Users.Add(new User
            {
                Username = "user",
                Email = "user@buecherausleihe.local",
                PasswordHash = _passwordHasher.Hash("User1234!"),
                Role = UserRole.Users
            });
            await _context.SaveChangesAsync();
        }

        if (!_context.Books.Any())
        {
            _context.Books.AddRange(
                new Book { Title = "Clean Code", Author = "Robert C. Martin", ISBN = "978-0132350884", TotalCopies = 3, AvailableCopies = 3 },
                new Book { Title = "The Pragmatic Programmer", Author = "David Thomas", ISBN = "978-0135957059", TotalCopies = 3, AvailableCopies = 3 },
                new Book { Title = "Design Patterns", Author = "Gang of Four", ISBN = "978-0201633610", TotalCopies = 3, AvailableCopies = 3 },
                new Book { Title = "The Clean Coder", Author = "Robert C. Martin", ISBN = "978-0137081073", TotalCopies = 3, AvailableCopies = 3 },
                new Book { Title = "Refactoring", Author = "Martin Fowler", ISBN = "978-0134757599", TotalCopies = 3, AvailableCopies = 3 },
                new Book { Title = "Domain-Driven Design", Author = "Eric Evans", ISBN = "978-0321125217", TotalCopies = 3, AvailableCopies = 3 },
                new Book { Title = "Introduction to Algorithms", Author = "Thomas H. Cormen", ISBN = "978-0262046305", TotalCopies = 3, AvailableCopies = 3 },
                new Book { Title = "Code Complete", Author = "Steve McConnell", ISBN = "978-0735619678", TotalCopies = 3, AvailableCopies = 3 },
                new Book { Title = "You Don't Know JS", Author = "Kyle Simpson", ISBN = "978-1491924464", TotalCopies = 3, AvailableCopies = 3 },
                new Book { Title = "The Mythical Man-Month", Author = "Frederick P. Brooks Jr.", ISBN = "978-0201835953", TotalCopies = 3, AvailableCopies = 3 }
            );
            await _context.SaveChangesAsync();
        }
    }
}
