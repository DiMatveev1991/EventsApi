using EventsApi.Application.Dtos;
using EventsApi.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventsApi.Presentation.Controllers
{
    /// <summary>Предоставляет HTTP API управления событиями и чтения рейтинга.</summary>
    [ApiController]
    [Route("events")]
    public class EventsController(IEventService eventService) : ControllerBase
    {
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
            var result = await eventService.GetAllAsync(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Получить топ-10 событий по проценту проданных мест</summary>
        [HttpGet("top")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IReadOnlyList<PopularEventDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<PopularEventDto>>> GetTop(
            CancellationToken cancellationToken)
        {
            var result = await eventService.GetTopPopularAsync(cancellationToken);
            return Ok(result);
        }

        /// <summary>Получить мероприятие по ID</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EventDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var ev = await eventService.GetByIdAsync(id, cancellationToken);
            return Ok(ev);
        }

        /// <summary>Создать новое мероприятие</summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<EventDto>> Create(
            [FromBody] CreateEventDto dto, CancellationToken cancellationToken)
        {
            var created = await eventService.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>Обновить мероприятие целиком</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EventDto>> Update(
            Guid id, [FromBody] UpdateEventDto dto, CancellationToken cancellationToken)
        {
            var updated = await eventService.UpdateAsync(id, dto, cancellationToken);
            return Ok(updated);
        }

        /// <summary>Удалить мероприятие</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await eventService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
    }
}
