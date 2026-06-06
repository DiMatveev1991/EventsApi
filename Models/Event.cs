using EventsApi.Exceptions;

namespace EventsApi.Models
{
	public class Event
	{
		private readonly object _seatsLock = new();

		public Guid Id { get; set; }
		public string Title { get; set; } = string.Empty;
		public string? Description { get; set; }
		public DateTime StartAt { get; set; }
		public DateTime EndAt { get; set; }

		/// <summary>Общее количество мест на событии.</summary>
		public int TotalSeats { get; set; }

		/// <summary>Текущее количество свободных мест.</summary>
		public int AvailableSeats { get; set; }

		/// <summary>
		/// Фабричный метод создания события. Валидирует totalSeats:
		/// значение должно быть больше нуля, иначе — <see cref="ValidationException"/>.
		/// При создании AvailableSeats равно TotalSeats.
		/// </summary>
		public static Event Create(
			string title,
			string? description,
			DateTime startAt,
			DateTime endAt,
			int totalSeats)
		{
			if (totalSeats <= 0)
				throw new ValidationException(
					"TotalSeats должен быть больше нуля",
					new Dictionary<string, string[]>
					{
						[nameof(TotalSeats)] = new[] { "TotalSeats должен быть больше нуля" }
					});

			return new Event
			{
				Id = Guid.NewGuid(),
				Title = title,
				Description = description,
				StartAt = startAt,
				EndAt = endAt,
				TotalSeats = totalSeats,
				AvailableSeats = totalSeats
			};
		}

		/// <summary>
		/// Пытается зарезервировать места: возвращает false, если свободных мест
		/// недостаточно; иначе уменьшает AvailableSeats на count и возвращает true.
		/// </summary>
		public bool TryReserveSeats(int count = 1)
		{
			lock (_seatsLock)
			{
				if (AvailableSeats < count)
					return false;

				AvailableSeats -= count;
				return true;
			}
		}

		/// <summary>Освобождает места (например, при отклонении брони).</summary>
		public void ReleaseSeats(int count = 1)
		{
			lock (_seatsLock)
			{
				AvailableSeats = Math.Min(TotalSeats, AvailableSeats + count);
			}
		}
	}
}