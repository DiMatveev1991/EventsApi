namespace EventsApi.Domain.Entities;

/// <summary>Inbox marker that makes BookingConfirmed processing idempotent.</summary>
public sealed class ProcessedBookingMessage
{
    private ProcessedBookingMessage() { }

    public Guid BookingId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
    public string Result { get; private set; } = string.Empty;

    public static ProcessedBookingMessage Create(Guid bookingId, string result) => new()
    {
        BookingId = bookingId,
        ProcessedAt = DateTimeOffset.UtcNow,
        Result = result
    };
}
