using System.ComponentModel.DataAnnotations;

namespace EventsApi.Application.Dtos
{
    /// <summary>Данные для создания события.</summary>
    public class CreateEventDto : EventWriteDto
    {
        /// <summary>Общее количество доступных при создании мест.</summary>
        [Required(ErrorMessage = "Поле TotalSeats обязательно")]
        [Range(1, int.MaxValue, ErrorMessage = "TotalSeats должен быть больше нуля")]
        public int? TotalSeats { get; set; }
    }
}
