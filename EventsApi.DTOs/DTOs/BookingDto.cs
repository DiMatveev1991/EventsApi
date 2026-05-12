namespace EventsApi.DTOs
{
    /// <summary>
    /// Информация о брони, возвращаемая API.
    /// </summary>
    public class BookingDto
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public BookingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
