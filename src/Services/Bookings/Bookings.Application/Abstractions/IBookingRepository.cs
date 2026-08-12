using Bookings.Domain.Entities;

namespace Bookings.Application.Abstractions;

/// <summary>Определяет операции хранилища бронирований.</summary>
public interface IBookingRepository
{
    /// <summary>Возвращает бронирование по идентификатору либо <c>null</c>.</summary>
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Возвращает идентификаторы ожидающих подтверждения бронирований.</summary>
    Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Возвращает подтверждённые, но ещё не опубликованные бронирования.</summary>
    Task<IReadOnlyList<Guid>> GetUnpublishedConfirmedIdsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Считает активные бронирования пользователя.</summary>
    Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Добавляет бронирование и сохраняет изменения.</summary>
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>Сохраняет изменения отслеживаемых сущностей.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
