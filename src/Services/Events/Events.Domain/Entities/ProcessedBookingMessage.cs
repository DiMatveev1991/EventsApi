namespace EventsApi.Domain.Entities;

/// <summary>Inbox marker that makes BookingConfirmed processing idempotent.</summary>
public sealed class ProcessedBookingMessage
{
    /// <summary>Создаёт пустой экземпляр для материализации EF Core.</summary>
    private ProcessedBookingMessage() { }

    /// <summary>Идентификатор бронирования и первичный ключ inbox.</summary>
    public Guid BookingId { get; private set; }

    /// <summary>Момент завершения обработки сообщения.</summary>
    public DateTimeOffset ProcessedAt { get; private set; }

    /// <summary>Результат применения сообщения.</summary>
    public string Result { get; private set; } = string.Empty;

    /// <summary>Создаёт inbox-маркер для атомарной фиксации результата обработки.</summary>
    public static ProcessedBookingMessage Create(Guid bookingId, string result) => new()
    {
        BookingId = bookingId,
        ProcessedAt = DateTimeOffset.UtcNow,
        Result = result
    };
}
