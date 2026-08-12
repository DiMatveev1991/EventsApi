using EventsApi.Application.Dtos;

namespace EventsApi.Application.Services
{
    /// <summary>Определяет прикладные сценарии управления событиями.</summary>
    public interface IEventService
    {
        /// <summary>
        /// Возвращает страницу событий с применёнными фильтрами.
        /// </summary>
        Task<PaginatedResult<EventDto>> GetAllAsync(
            EventQueryParameters query, CancellationToken cancellationToken = default);

        /// <summary>Возвращает событие по идентификатору с использованием кеша.</summary>
        Task<EventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Возвращает до десяти событий с наибольшим процентом продаж.</summary>
        Task<IReadOnlyList<PopularEventDto>> GetTopPopularAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Создаёт новое событие.</summary>
        Task<EventDto> CreateAsync(CreateEventDto dto, CancellationToken cancellationToken = default);

        /// <summary>Полностью обновляет существующее событие.</summary>
        Task<EventDto> UpdateAsync(Guid id, UpdateEventDto dto, CancellationToken cancellationToken = default);

        /// <summary>Удаляет событие.</summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
