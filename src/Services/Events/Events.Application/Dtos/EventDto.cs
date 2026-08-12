using System.ComponentModel.DataAnnotations;

namespace EventsApi.Application.Dtos
{
    public class EventDto
    {
        public Guid Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTimeOffset StartAt { get; set; }
        public DateTimeOffset EndAt { get; set; }

        /// <summary>Общее количество мест на событии.</summary>
        public int TotalSeats { get; set; }

        /// <summary>Текущее количество доступных мест.</summary>
        public int AvailableSeats { get; set; }
    }
}
