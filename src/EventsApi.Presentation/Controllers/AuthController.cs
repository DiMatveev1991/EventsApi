using EventsApi.Application.Dtos;
using EventsApi.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventsApi.Presentation.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>Зарегистрировать пользователя.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserDto dto,
        CancellationToken cancellationToken)
    {
        await _userService.RegisterAsync(dto, cancellationToken);
        return NoContent();
    }

    /// <summary>Получить JWT по логину и паролю.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TokenDto>> Login(
        [FromBody] LoginDto dto,
        CancellationToken cancellationToken)
    {
        var token = await _userService.LoginAsync(dto, cancellationToken);
        return Ok(token);
    }
}
