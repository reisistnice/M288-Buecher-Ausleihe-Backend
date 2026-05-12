using Core.Entities;

namespace Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByNameAsync(string username);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> CreateUserAsync(string email, string username, string passwordHash, UserRole role);
}
