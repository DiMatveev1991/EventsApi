using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Application.Dtos;
using Users.Application.Services;

namespace Users.Presentation.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
public sealed class AuthController(IUserService users) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        await users.RegisterAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TokenResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        Ok(await users.LoginAsync(request, cancellationToken));
}
