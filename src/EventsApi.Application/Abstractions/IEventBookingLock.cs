namespace EventsApi.Application.Abstractions;

/// <summary>
/// Сериализует операции бронирования отдельно для каждого события.
/// </summary>
public interface IEventBookingLock
{
    /// <summary>
    /// Захватывает блокировку события. Возвращённый объект освобождает её при Dispose.
    /// </summary>
    ValueTask<IDisposable> AcquireAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);
}
