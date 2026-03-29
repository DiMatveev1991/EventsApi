namespace EventsApi.Models
{
	public class Event
	{
		public Guid Id { get; set; }
		public string Title { get; set; } = string.Empty;
		public string? Description { get; set; }
		public DateTime StartAt { get; set; }
		public DateTime EndAt { get; set; }
	}
}