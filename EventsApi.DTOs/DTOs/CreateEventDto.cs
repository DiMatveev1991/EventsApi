using System.ComponentModel.DataAnnotations;

namespace EventsApi.DTOs
{
	public class CreateEventDto : EventWriteDto
	{
		[Required(ErrorMessage = "ѕоле TotalSeats об€зательно")]
		[Range(1, int.MaxValue, ErrorMessage = "TotalSeats должен быть больше нул€")]
		public int? TotalSeats { get; set; }
	}
}