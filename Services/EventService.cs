using EventsApi.DTOs;
using EventsApi.Models;

namespace EventsApi.Services
{
	public class EventService : IEventService
	{
		private readonly List<Event> _events = new();

		public IReadOnlyList<EventDto> GetAll()
		{
			return _events.Select(MapToDto).ToList();
		}

		public EventDto? GetById(Guid id)
		{
			var ev = _events.FirstOrDefault(e => e.Id == id);
			return ev is null ? null : MapToDto(ev);
		}

		public EventDto Create(CreateEventDto dto)
		{
			var ev = new Event
			{
				Id = Guid.NewGuid(),
				Title = dto.Title,
				Description = dto.Description,
				StartAt = dto.StartAt,
				EndAt = dto.EndAt
			};

			_events.Add(ev);
			return MapToDto(ev);
		}

		public EventDto? Update(Guid id, UpdateEventDto dto)
		{
			var ev = _events.FirstOrDefault(e => e.Id == id);
			if (ev is null)
				return null;

			ev.Title = dto.Title;
			ev.Description = dto.Description;
			ev.StartAt = dto.StartAt;
			ev.EndAt = dto.EndAt;

			return MapToDto(ev);
		}

		public bool Delete(Guid id)
		{
			var ev = _events.FirstOrDefault(e => e.Id == id);
			if (ev is null)
				return false;

			_events.Remove(ev);
			return true;
		}

		private static EventDto MapToDto(Event ev) => new()
		{
			Id = ev.Id,
			Title = ev.Title,
			Description = ev.Description,
			StartAt = ev.StartAt,
			EndAt = ev.EndAt
		};
	}
}