using System.ComponentModel.DataAnnotations;

namespace EventsApi.DTOs
{
	public class CreateEventDto : IValidatableObject
	{
		[Required(ErrorMessage = "Поле Title обязательно")]
		[MinLength(1, ErrorMessage = "Title не может быть пустым")]
		public string Title { get; set; } = string.Empty;

		public string? Description { get; set; }

		[Required(ErrorMessage = "Поле StartAt обязательно")]
		public DateTime StartAt { get; set; }

		[Required(ErrorMessage = "Поле EndAt обязательно")]
		public DateTime EndAt { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (EndAt <= StartAt)
				yield return new ValidationResult(
					"EndAt должен быть позже StartAt",
					new[] { nameof(EndAt) });
		}
	}
}