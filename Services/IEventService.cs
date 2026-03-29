

using EventsApi.DTOs;

namespace EventsApi.Services
{
	public interface IEventService
	{
		IReadOnlyList<EventDto> GetAll();
		EventDto? GetById(Guid id);
		EventDto Create(CreateEventDto dto);
		EventDto? Update(Guid id, UpdateEventDto dto);
		bool Delete(Guid id);
	}
}
