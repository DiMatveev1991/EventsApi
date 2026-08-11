using System.Security.Claims;
using EventsApi.Application.Dtos;
using EventsApi.Application.Services;
using EventsApi.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventsApi.Presentation.Controllers
{
    [ApiController]
    [Authorize]
    [Route("bookings")]
    public class BookingsController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingsController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        /// <summary>
        /// Получить текущее состояние брони по идентификатору.
        /// </summary>
        [HttpGet("{id:guid}", Name = "GetBookingById")]
        [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BookingDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var booking = await _bookingService.GetBookingByIdAsync(id, cancellationToken);
            return Ok(booking);
        }

        /// <summary>Отменить бронь.</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAdmin = User.IsInRole(nameof(UserRole.Admin));

            await _bookingService.CancelBookingAsync(id, userId, isAdmin, cancellationToken);
            return NoContent();
        }
    }
}
