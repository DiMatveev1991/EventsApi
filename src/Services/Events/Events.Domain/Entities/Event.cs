using EventsApi.Domain.Exceptions;

namespace EventsApi.Domain.Entities
{
    /// <summary>Доменная сущность события с учётом вместимости.</summary>
    public class Event
    {
        // Приватный конструктор без параметров нужен EF Core: провайдер создаёт
        // экземпляры через рефлексию при чтении данных из БД.
        /// <summary>Создаёт пустой экземпляр для материализации EF Core.</summary>
        private Event() { }

        /// <summary>Идентификатор события.</summary>
        public Guid Id { get; set; }

        /// <summary>Название события.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Необязательное описание события.</summary>
        public string? Description { get; set; }

        /// <summary>Дата и время начала события.</summary>
        public DateTimeOffset StartAt { get; set; }

        /// <summary>Дата и время окончания события.</summary>
        public DateTimeOffset EndAt { get; set; }

        /// <summary>Общее количество мест на событии.</summary>
        public int TotalSeats { get; set; }

        /// <summary>Текущее количество доступных мест.</summary>
        public int AvailableSeats { get; set; }

        /// <summary>
        /// Фабричный метод создания события. Валидирует totalSeats:
        /// значение должно быть больше нуля, иначе — <see cref="ValidationException"/>.
        /// При создании AvailableSeats равно TotalSeats.
        /// </summary>
        public static Event Create(
            string title,
            string? description,
            DateTimeOffset startAt,
            DateTimeOffset endAt,
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
                StartAt = startAt.ToUniversalTime(),
                EndAt = endAt.ToUniversalTime(),
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
            if (count <= 0)
                return false;

            if (AvailableSeats < count)
                return false;

            AvailableSeats -= count;
            return true;
        }
    }
}
