using EventsApi.DTOs;
using EventsApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventsApi.Controllers
{
    [ApiController]
    [Route("events")]
    [Produces("application/json")]
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
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<EventDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public ActionResult<PaginatedResult<EventDto>> GetAll([FromQuery] EventQueryParameters query)
        {
            var result = _eventService.GetAll(query);
            return Ok(result);
        }

        /// <summary>Получить мероприятие по ID</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public ActionResult<EventDto> GetById(Guid id)
        {
            var ev = _eventService.GetById(id);
            return Ok(ev);
        }

        /// <summary>Создать новое мероприятие</summary>
        [HttpPost]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public ActionResult<EventDto> Create([FromBody] CreateEventDto dto)
        {
            var created = _eventService.Create(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>Обновить мероприятие целиком</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public ActionResult<EventDto> Update(Guid id, [FromBody] UpdateEventDto dto)
        {
            var updated = _eventService.Update(id, dto);
            return Ok(updated);
        }

        /// <summary>Удалить мероприятие</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public IActionResult Delete(Guid id)
        {
            _eventService.Delete(id);
            return NoContent();
        }

        /// <summary>
        /// Создать бронь для мероприятия. Возвращает 202 Accepted сразу;
        /// статус брони обновляется фоновым сервисом.
        /// </summary>
        [HttpPost("{id:guid}/book")]
        [ProducesResponseType(typeof(BookingDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
