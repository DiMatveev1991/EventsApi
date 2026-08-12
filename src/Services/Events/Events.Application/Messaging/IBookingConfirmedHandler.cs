using Contracts;

namespace EventsApi.Application.Messaging;

/// <summary>Определяет сценарий обработки подтверждённого бронирования.</summary>
public interface IBookingConfirmedHandler
{
    /// <summary>Применяет сообщение и возвращает результат его обработки.</summary>
    Task<BookingConfirmationResult> HandleAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default);
}
