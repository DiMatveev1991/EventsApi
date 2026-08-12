using EventsApi.Domain.Entities;
using Contracts;
using EventsApi.Application.Messaging;

namespace EventsApi.Application.Abstractions
{
    /// <summary>
    /// Порт доступа к данным событий. Абстракция, которую Application определяет
    /// для инфраструктуры: сервисы работают с событиями только через этот интерфейс
    /// и ничего не знают о конкретном хранилище (реализация — в слое Infrastructure).
    /// </summary>
    public interface IEventRepository
    {
        /// <summary>
        /// Возвращает страницу событий с применёнными фильтрами и общее число
        /// подходящих под фильтр записей (до пагинации). Фильтр по названию —
        /// регистронезависимое частичное совпадение; <paramref name="from"/> и
        /// <paramref name="to"/> ограничивают дату начала/окончания.
        /// </summary>
        Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
            string? title,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int skip,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает событие по идентификатору или <c>null</c>, если оно не найдено.
        /// Сущность отслеживается контекстом, поэтому её можно изменять и сохранять
        /// через <see cref="UpdateAsync"/>.
        /// </summary>
        Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает не более десяти событий с наибольшей долей проданных мест.
        /// </summary>
        Task<IReadOnlyList<Event>> GetTopPopularAsync(
            int count,
            CancellationToken cancellationToken = default);

        /// <summary>Добавляет новое событие и сохраняет изменения.</summary>
        Task AddAsync(Event ev, CancellationToken cancellationToken = default);

        /// <summary>Сохраняет изменения ранее полученного события.</summary>
        /// <remarks>
        /// Переданный экземпляр должен быть получен через <see cref="GetByIdAsync"/>
        /// в том же DI-scope и отслеживаться тем же контекстом репозитория.
        /// </remarks>
        Task UpdateAsync(Event ev, CancellationToken cancellationToken = default);

        /// <summary>Удаляет событие и сохраняет изменения.</summary>
        Task DeleteAsync(Event ev, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically applies an integration event and records its BookingId in the inbox.
        /// </summary>
        Task<BookingConfirmationResult> ApplyBookingConfirmedAsync(
            BookingConfirmed message,
            CancellationToken cancellationToken = default);
    }
}
