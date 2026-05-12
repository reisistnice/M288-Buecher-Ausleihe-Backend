using Application.Common.Interfaces;
using Application.Features.Auth.DTOs;

namespace Application.Features.Auth.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(IUserRepository userRepository, IJwtTokenGenerator jwt, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _jwt = jwt;
        _passwordHasher = passwordHasher;
    }

    public async Task<TokenResponseDto?> LoginUser(string username, string password)
    {
        var user = await _userRepository.GetUserByNameAsync(username);
        if (user is null || !_passwordHasher.Verify(password, user.PasswordHash))
            return null;

        return new TokenResponseDto(_jwt.GenerateToken(user));
    }

    public async Task<(TokenResponseDto? Token, string? Error)> RegisterUser(string email, string username, string password)
    {
        if (await _userRepository.GetUserByEmailAsync(email) is not null)
            return (null, "Email already taken.");

        if (await _userRepository.GetUserByNameAsync(username) is not null)
            return (null, "Username already taken.");

        var hash = _passwordHasher.Hash(password);
        var user = await _userRepository.CreateUserAsync(email, username, hash, Core.Entities.UserRole.Users);
        if (user is null)
            return (null, "Registration failed.");

        return (new TokenResponseDto(_jwt.GenerateToken(user)), null);
    }
}
