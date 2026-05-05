using Application.Common.Interfaces;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByNameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> CreateUserAsync(string username, string passwordHash, UserRole role)
    {
        var user = new User { Username = username, PasswordHash = passwordHash, Role = role };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }
}
