using System.ComponentModel.DataAnnotations;

namespace EventsApi.Application.Dtos
{
    /// <summary>Общие поля для создания и обновления мероприятия.</summary>
    public abstract class EventWriteDto : IValidatableObject
    {
        /// <summary>Название события.</summary>
        [Required(ErrorMessage = "Поле Title обязательно")]
        [MinLength(1, ErrorMessage = "Title не может быть пустым")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Необязательное описание события.</summary>
        public string? Description { get; set; }

        /// <summary>Дата и время начала события.</summary>
        [Required(ErrorMessage = "Поле StartAt обязательно")]
        public DateTimeOffset StartAt { get; set; }

        /// <summary>Дата и время окончания события.</summary>
        [Required(ErrorMessage = "Поле EndAt обязательно")]
        public DateTimeOffset EndAt { get; set; }

        /// <summary>Проверяет, что окончание события следует после его начала.</summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndAt <= StartAt)
                yield return new ValidationResult(
                    "EndAt должен быть позже StartAt",
                    new[] { nameof(EndAt) });
        }
    }
}
