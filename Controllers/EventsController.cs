

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

        public EventsController(IEventService eventService)
        {
            _eventService = eventService;
        }

        /// <summary>Получить список всех мероприятий</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<EventDto>), StatusCodes.Status200OK)]
        public ActionResult<IReadOnlyList<EventDto>> GetAll()
        {
            return Ok(_eventService.GetAll());
        }

        /// <summary>Получить мероприятие по ID</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<EventDto> GetById(Guid id)
        {
            var ev = _eventService.GetById(id);
            if (ev is null)
                return NotFound(new { message = $"Мероприятие с ID {id} не найдено" });

            return Ok(ev);
        }

        /// <summary>Создать новое мероприятие</summary>
        [HttpPost]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<EventDto> Create([FromBody] CreateEventDto dto)
        {
            var created = _eventService.Create(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        /// <summary>Обновить мероприятие целиком</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<EventDto> Update(Guid id, [FromBody] UpdateEventDto dto)
        {
            var updated = _eventService.Update(id, dto);
            if (updated is null)
                return NotFound(new { message = $"Мероприятие с ID {id} не найдено" });

            return Ok(updated);
        }

        /// <summary>Удалить мероприятие</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult Delete(Guid id)
        {
            var deleted = _eventService.Delete(id);
            if (!deleted)
                return NotFound(new { message = $"Мероприятие с ID {id} не найдено" });

            return NoContent();
        }
    }
}