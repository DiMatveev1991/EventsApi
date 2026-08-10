using EventsApi.Application.Dtos;
using EventsApi.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventsApi.Presentation.Controllers
{
    [ApiController]
    [Route("events")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;
        private readonly IBookingService _bookingService;

        public EventsController(IEventService eventService, IBookingService bookingService)
        {
            _eventService = eventService;
            _bookingService = bookingService;
        }

        /// <summary>
        /// Получить список мероприятий с фильтрацией и пагинацией.
        /// </summary>
        /// <param name="query">Параметры фильтрации (title, from, to) и пагинации (page, pageSize).</param>
        /// <param name="cancellationToken">Токен отмены запроса.</param>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<EventDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginatedResult<EventDto>>> GetAll(
            [FromQuery] EventQueryParameters query, CancellationToken cancellationToken)
        {
            var result = await _eventService.GetAllAsync(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Получить мероприятие по ID</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EventDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var ev = await _eventService.GetByIdAsync(id, cancellationToken);
            return Ok(ev);
        }

        /// <summary>Создать новое мероприятие</summary>
        [HttpPost]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<EventDto>> Create(
            [FromBody] CreateEventDto dto, CancellationToken cancellationToken)
        {
            var created = await _eventService.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>Обновить мероприятие целиком</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EventDto>> Update(
            Guid id, [FromBody] UpdateEventDto dto, CancellationToken cancellationToken)
        {
            var updated = await _eventService.UpdateAsync(id, dto, cancellationToken);
            return Ok(updated);
        }

        /// <summary>Удалить мероприятие</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _eventService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Создать бронь для мероприятия. Возвращает 202 Accepted сразу;
        /// статус брони обновляется фоновым сервисом.
        /// Возвращает 409 Conflict, если на событии не осталось свободных мест.
        /// </summary>
        [HttpPost("{id:guid}/book")]
        [ProducesResponseType(typeof(BookingDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BookingDto>> Book(Guid id, CancellationToken cancellationToken)
        {
            var booking = await _bookingService.CreateBookingAsync(id, cancellationToken);

            var locationUri = Url.Action(
                action: nameof(BookingsController.GetById),
                controller: "Bookings",
                values: new { id = booking.Id }) ?? $"/bookings/{booking.Id}";

            // 202 Accepted + заголовок Location на ресурс брони + тело с информацией о брони.
            return Accepted(locationUri, booking);
        }
    }
}
