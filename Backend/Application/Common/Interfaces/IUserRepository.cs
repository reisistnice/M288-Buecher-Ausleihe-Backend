using Core.Entities;

namespace Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByNameAsync(string username);
    Task<User?> CreateUserAsync(string username, string passwordHash, UserRole role);
}
