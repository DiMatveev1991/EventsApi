namespace Bookings.Domain.Enums;

/// <summary>Состояние жизненного цикла бронирования.</summary>
public enum BookingStatus
{
    /// <summary>Бронирование ожидает фонового подтверждения.</summary>
    Pending = 0,

    /// <summary>Бронирование подтверждено.</summary>
    Confirmed = 1,

    /// <summary>Бронирование отменено.</summary>
    Cancelled = 2
}
