using System.ComponentModel.DataAnnotations;

namespace EventsApi.DTOs
{
	public class EventDto
	{
		public Guid Id { get; set; }

		[Required]
		public string Title { get; set; } = string.Empty;
		public string? Description { get; set; }
		public DateTime StartAt { get; set; }
		public DateTime EndAt { get; set; }

		/// <summary>Общее количество мест на событии.</summary>
		public int TotalSeats { get; set; }

		/// <summary>Текущее количество свободных мест.</summary>
		public int AvailableSeats { get; set; }
	}
}