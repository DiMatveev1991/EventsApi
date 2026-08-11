using System.Security.Claims;
using Bookings.Application.Dtos;
using Bookings.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("bookings")]
public sealed class BookingsController(IBookingService bookings) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BookingResponse>> Create(
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var response = await bookings.CreateAsync(request, CurrentUserId(), cancellationToken);
        return AcceptedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await bookings.GetByIdAsync(
            id,
            CurrentUserId(),
            User.IsInRole("Admin"),
            cancellationToken);
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await bookings.CancelAsync(
            id,
            CurrentUserId(),
            User.IsInRole("Admin"),
            cancellationToken);
        return NoContent();
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new UnauthorizedAccessException("JWT does not contain a valid user identifier.");
}
