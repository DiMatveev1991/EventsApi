namespace EventsApi.Application.Messaging;

/// <summary>Результат применения подтверждённого бронирования к событию.</summary>
public enum BookingConfirmationResult
{
    /// <summary>Места успешно зарезервированы.</summary>
    Applied,

    /// <summary>Сообщение уже было обработано.</summary>
    Duplicate,

    /// <summary>Связанное событие не найдено.</summary>
    EventNotFound,

    /// <summary>Для бронирования недостаточно свободных мест.</summary>
    InsufficientSeats
}
