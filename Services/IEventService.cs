using EventsApi.DTOs;

namespace EventsApi.Services
{
    public interface IEventService
    {
        /// <summary>
        /// Возвращает страницу событий с применёнными фильтрами.
        /// </summary>
        Task<PaginatedResult<EventDto>> GetAllAsync(
            EventQueryParameters query, CancellationToken cancellationToken = default);

        Task<EventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<EventDto> CreateAsync(CreateEventDto dto, CancellationToken cancellationToken = default);
        Task<EventDto> UpdateAsync(Guid id, UpdateEventDto dto, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
