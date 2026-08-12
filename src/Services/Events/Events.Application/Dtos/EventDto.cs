using System.ComponentModel.DataAnnotations;

namespace EventsApi.Application.Dtos
{
    /// <summary>Полное представление события для API.</summary>
    public class EventDto
    {
        /// <summary>Идентификатор события.</summary>
        public Guid Id { get; set; }

        /// <summary>Название события.</summary>
        [Required]
        public string Title { get; set; } = string.Empty;

        /// <summary>Необязательное описание события.</summary>
        public string? Description { get; set; }

        /// <summary>Дата и время начала события.</summary>
        public DateTimeOffset StartAt { get; set; }

        /// <summary>Дата и время окончания события.</summary>
        public DateTimeOffset EndAt { get; set; }

        /// <summary>Общее количество мест на событии.</summary>
        public int TotalSeats { get; set; }

        /// <summary>Текущее количество доступных мест.</summary>
        public int AvailableSeats { get; set; }
    }
}
