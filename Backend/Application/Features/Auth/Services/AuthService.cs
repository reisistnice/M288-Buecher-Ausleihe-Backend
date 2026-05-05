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
}
