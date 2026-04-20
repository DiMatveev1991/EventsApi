using EventsApi.DTOs;

namespace EventsApi.Services
{
    public interface IEventService
    {
        /// <summary>
        /// Возвращает страницу событий с применёнными фильтрами.
        /// </summary>
        PaginatedResult<EventDto> GetAll(EventQueryParameters query);

        EventDto GetById(Guid id);
        EventDto Create(CreateEventDto dto);
        EventDto Update(Guid id, UpdateEventDto dto);
        void Delete(Guid id);
    }
}
