using EventsApi.Domain.Entities;

namespace EventsApi.Application.Abstractions
{
    /// <summary>
    /// Порт доступа к данным бронирований. Абстракция, которую Application определяет
    /// для инфраструктуры: сервисы и фоновый обработчик работают с бронями только
    /// через этот интерфейс (реализация — в слое Infrastructure).
    /// </summary>
    public interface IBookingRepository
    {
        /// <summary>
        /// Возвращает бронь по идентификатору или <c>null</c>. Сущность отслеживается
        /// контекстом, поэтому её можно изменять и сохранять через <see cref="UpdateAsync"/>.
        /// </summary>
        Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает идентификаторы всех броней в статусе <c>Pending</c> —
        /// используется фоновым обработчиком для выборки задач.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default);

        /// <summary>Добавляет новую бронь и сохраняет изменения.</summary>
        Task AddAsync(Booking booking, CancellationToken cancellationToken = default);

        /// <summary>Сохраняет изменения ранее полученной (отслеживаемой) брони.</summary>
        Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default);
    }
}
