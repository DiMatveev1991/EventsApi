using Contracts;

namespace Bookings.Application.Abstractions;

/// <summary>Определяет порт публикации событий бронирования.</summary>
public interface IBookingEventPublisher
{
    /// <summary>Публикует подтверждённое бронирование во внешний брокер сообщений.</summary>
    Task PublishAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default);
}
