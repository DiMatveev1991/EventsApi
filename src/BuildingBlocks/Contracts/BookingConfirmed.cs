namespace Contracts;

/// <summary>Public integration event emitted after a booking is confirmed.</summary>
public sealed record BookingConfirmed(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int Seats,
    DateTimeOffset ConfirmedAt);
