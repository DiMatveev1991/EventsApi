using Bookings.Domain.Enums;

namespace Bookings.Application.Dtos;

public sealed record BookingResponse(
    Guid Id,
    Guid EventId,
    Guid UserId,
    int Seats,
    BookingStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);
