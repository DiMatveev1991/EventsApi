using EventsApi.Application.Dtos;
using EventsApi.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventsApi.Presentation.Controllers
{
    [ApiController]
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
    }
}
