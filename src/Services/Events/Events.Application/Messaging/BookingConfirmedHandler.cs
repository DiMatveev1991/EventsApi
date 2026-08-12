using Contracts;
using EventsApi.Application.Abstractions;
using EventsApi.Application.Caching;

namespace EventsApi.Application.Messaging;

/// <summary>
/// Применяет подтверждение бронирования и инвалидирует кеш изменённого события.
/// </summary>
public sealed class BookingConfirmedHandler(
    IEventRepository events,
    ICacheService cache) : IBookingConfirmedHandler
{
    /// <summary>Обрабатывает интеграционное событие с соблюдением порядка БД → кеш.</summary>
    public async Task<BookingConfirmationResult> HandleAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default)
    {
        var result = await events.ApplyBookingConfirmedAsync(message, cancellationToken);

        // Репозиторий к этому моменту уже зафиксировал изменение и inbox-запись.
        if (result == BookingConfirmationResult.Applied)
            await cache.RemoveAsync(CacheKeys.Event(message.EventId), cancellationToken);

        return result;
    }
}
