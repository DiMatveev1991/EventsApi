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
	}
}