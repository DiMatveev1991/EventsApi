namespace Contracts;

/// <summary>
/// Интеграционное событие, публикуемое после подтверждения бронирования.
/// </summary>
/// <param name="BookingId">Идентификатор бронирования и ключ идемпотентности.</param>
/// <param name="EventId">Идентификатор события, для которого резервируются места.</param>
/// <param name="UserId">Идентификатор пользователя, создавшего бронирование.</param>
/// <param name="Seats">Количество зарезервированных мест.</param>
/// <param name="ConfirmedAt">Момент подтверждения бронирования в UTC.</param>
public sealed record BookingConfirmed(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int Seats,
    DateTimeOffset ConfirmedAt);
