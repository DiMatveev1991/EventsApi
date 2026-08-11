using Bookings.Domain.Enums;
using Bookings.Domain.Exceptions;

namespace Bookings.Domain.Entities;

public sealed class Booking
{
    private Booking() { }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public int Seats { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset? ConfirmationPublishedAt { get; private set; }

    public static Booking CreatePending(Guid eventId, Guid userId, int seats)
    {
        if (eventId == Guid.Empty)
            throw new ValidationException("EventId is required.");
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");
        if (seats is < 1 or > 50)
            throw new ValidationException("Seats must be between 1 and 50.");

        return new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Seats = seats,
            Status = BookingStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Confirm(DateTimeOffset confirmedAt)
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException("Only a pending booking can be confirmed.");

        Status = BookingStatus.Confirmed;
        ProcessedAt = confirmedAt.ToUniversalTime();
    }

    public void MarkConfirmationPublished(DateTimeOffset publishedAt)
    {
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("Only a confirmed booking can be published.");

        ConfirmationPublishedAt = publishedAt.ToUniversalTime();
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status == BookingStatus.Cancelled)
            throw new ValidationException("Booking is already cancelled.");

        Status = BookingStatus.Cancelled;
        ProcessedAt = cancelledAt.ToUniversalTime();
    }
}
