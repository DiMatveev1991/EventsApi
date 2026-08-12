using Bookings.Domain.Enums;

namespace Bookings.Application.Dtos;

/// <summary>Представление бронирования, возвращаемое API.</summary>
/// <param name="Id">Идентификатор бронирования.</param>
/// <param name="EventId">Идентификатор события.</param>
/// <param name="UserId">Идентификатор владельца.</param>
/// <param name="Seats">Количество мест.</param>
/// <param name="Status">Текущий статус.</param>
/// <param name="CreatedAt">Момент создания.</param>
/// <param name="ProcessedAt">Момент подтверждения или отмены.</param>
public sealed record BookingResponse(
    Guid Id,
    Guid EventId,
    Guid UserId,
    int Seats,
    BookingStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);
