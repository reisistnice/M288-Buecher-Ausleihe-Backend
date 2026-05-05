using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Common.Interfaces;
using Application.Common.Settings;
using Core.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _jwtSettings;

    public JwtTokenGenerator(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string GenerateToken(User? user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new ClaimsIdentity();
        claims.AddClaim(new Claim(ClaimTypes.Name, user.Username));
        claims.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        claims.AddClaim(new Claim(ClaimTypes.SerialNumber, user.Id.ToString()));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = claims,
            SigningCredentials = creds,
            Expires = DateTime.UtcNow.AddHours(_jwtSettings.ExpiresIn),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }
}
