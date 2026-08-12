namespace EventsApi.Application.Dtos;

/// <summary>Событие из рейтинга с рассчитанным процентом проданных мест.</summary>
public sealed class PopularEventDto
{
    /// <summary>Идентификатор события.</summary>
    public Guid Id { get; set; }

    /// <summary>Название события.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Необязательное описание события.</summary>
    public string? Description { get; set; }

    /// <summary>Дата и время начала события.</summary>
    public DateTimeOffset StartAt { get; set; }

    /// <summary>Дата и время окончания события.</summary>
    public DateTimeOffset EndAt { get; set; }

    /// <summary>Общее количество мест.</summary>
    public int TotalSeats { get; set; }

    /// <summary>Количество оставшихся мест.</summary>
    public int AvailableSeats { get; set; }

    /// <summary>Процент проданных мест.</summary>
    public double SoldPercentage { get; set; }
}
