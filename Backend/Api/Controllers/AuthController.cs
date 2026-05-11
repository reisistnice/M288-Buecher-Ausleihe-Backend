using Application.Features.Auth.DTOs;
using Application.Features.Auth.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginUser([FromBody] LoginUserDto loginUserDto)
    {
        var tokenResponse = await _authService.LoginUser(loginUserDto.Username, loginUserDto.Password);
        if (tokenResponse is null)
            return Unauthorized();

        return Ok(tokenResponse);
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterUserDto registerUserDto)
    {
        var tokenResponse = await _authService.RegisterUser(registerUserDto.Username, registerUserDto.Password);
        if (tokenResponse is null)
            return Conflict(new { message = "Username already taken." });

        return Ok(tokenResponse);
    }
}
