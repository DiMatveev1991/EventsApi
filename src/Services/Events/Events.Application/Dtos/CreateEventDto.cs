using System.ComponentModel.DataAnnotations;

namespace EventsApi.Application.Dtos
{
    public class CreateEventDto : EventWriteDto
    {
        [Required(ErrorMessage = "Поле TotalSeats обязательно")]
        [Range(1, int.MaxValue, ErrorMessage = "TotalSeats должен быть больше нуля")]
        public int? TotalSeats { get; set; }
    }
}
