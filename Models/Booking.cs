using EventsApi.DTOs;

namespace EventsApi.Models
{
    /// <summary>
    /// Бронь на мероприятие.
    /// Жизненный цикл: Pending → Confirmed | Rejected.
    /// </summary>
    public class Booking
    {
        // Приватный конструктор без параметров нужен EF Core: провайдер создаёт
        // экземпляры через рефлексию при чтении данных из БД.
        private Booking() { }

        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public BookingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        /// <summary>Навигационное свойство: событие, к которому относится бронь.</summary>
        public Event? Event { get; set; }

        /// <summary>
        /// Создаёт новую бронь в статусе Pending с сгенерированным Id и текущим UTC-временем.
        /// </summary>
        public static Booking CreatePending(Guid eventId) => new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };

        /// <summary>
        /// Переводит бронь в статус Confirmed и фиксирует момент обработки.
        /// </summary>
        public void Confirm(DateTime processedAt)
        {
            EnsurePending();
            Status = BookingStatus.Confirmed;
            ProcessedAt = processedAt;
        }

        /// <summary>
        /// Переводит бронь в статус Rejected и фиксирует момент обработки.
        /// </summary>
        public void Reject(DateTime processedAt)
        {
            EnsurePending();
            Status = BookingStatus.Rejected;
            ProcessedAt = processedAt;
        }

        private void EnsurePending()
        {
            if (Status != BookingStatus.Pending)
                throw new InvalidOperationException(
                    $"Переход статуса возможен только из Pending. Текущий статус: {Status}.");
        }
    }
}
