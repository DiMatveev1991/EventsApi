using System.ComponentModel.DataAnnotations;

namespace EventsApi.Application.Dtos
{
    /// <summary>Общие поля для создания и обновления мероприятия.</summary>
    public abstract class EventWriteDto : IValidatableObject
    {
        [Required(ErrorMessage = "Поле Title обязательно")]
        [MinLength(1, ErrorMessage = "Title не может быть пустым")]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Поле StartAt обязательно")]
        public DateTimeOffset StartAt { get; set; }

        [Required(ErrorMessage = "Поле EndAt обязательно")]
        public DateTimeOffset EndAt { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndAt <= StartAt)
                yield return new ValidationResult(
                    "EndAt должен быть позже StartAt",
                    new[] { nameof(EndAt) });
        }
    }
}
