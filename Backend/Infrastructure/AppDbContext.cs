using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Loan> Loans => Set<Loan>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
