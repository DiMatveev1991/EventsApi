using Bookings.Domain.Enums;
using Bookings.Domain.Exceptions;

namespace Bookings.Domain.Entities;

/// <summary>Доменная сущность бронирования с контролируемыми переходами статуса.</summary>
public sealed class Booking
{
    /// <summary>Создаёт пустой экземпляр для материализации EF Core.</summary>
    private Booking() { }

    /// <summary>Идентификатор бронирования.</summary>
    public Guid Id { get; private set; }

    /// <summary>Идентификатор связанного события.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Идентификатор владельца бронирования.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Количество забронированных мест.</summary>
    public int Seats { get; private set; }

    /// <summary>Текущий статус бронирования.</summary>
    public BookingStatus Status { get; private set; }

    /// <summary>Момент создания в UTC.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Момент подтверждения или отмены в UTC.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Момент успешной публикации подтверждения в Kafka.</summary>
    public DateTimeOffset? ConfirmationPublishedAt { get; private set; }

    /// <summary>Создаёт ожидающее бронирование после проверки входных данных.</summary>
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

    /// <summary>Переводит ожидающее бронирование в подтверждённое.</summary>
    public void Confirm(DateTimeOffset confirmedAt)
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException("Only a pending booking can be confirmed.");

        Status = BookingStatus.Confirmed;
        ProcessedAt = confirmedAt.ToUniversalTime();
    }

    /// <summary>Отмечает, что интеграционное событие успешно опубликовано.</summary>
    public void MarkConfirmationPublished(DateTimeOffset publishedAt)
    {
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("Only a confirmed booking can be published.");

        ConfirmationPublishedAt = publishedAt.ToUniversalTime();
    }

    /// <summary>Отменяет бронирование и фиксирует время обработки.</summary>
    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status == BookingStatus.Cancelled)
            throw new ValidationException("Booking is already cancelled.");

        Status = BookingStatus.Cancelled;
        ProcessedAt = cancelledAt.ToUniversalTime();
    }
}
