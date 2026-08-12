namespace EventsApi.Application.Dtos;

/// <summary>Событие из рейтинга с рассчитанным процентом проданных мест.</summary>
public sealed class PopularEventDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public double SoldPercentage { get; set; }
}
