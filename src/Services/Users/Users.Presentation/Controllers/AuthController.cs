using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Application.Dtos;
using Users.Application.Services;

namespace Users.Presentation.Controllers;

/// <summary>Предоставляет публичные эндпоинты регистрации и входа.</summary>
[ApiController]
[AllowAnonymous]
[Route("auth")]
public sealed class AuthController(IUserService users) : ControllerBase
{
    /// <summary>Регистрирует нового пользователя.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        await users.RegisterAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Проверяет учётные данные и возвращает JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TokenResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        Ok(await users.LoginAsync(request, cancellationToken));
}
