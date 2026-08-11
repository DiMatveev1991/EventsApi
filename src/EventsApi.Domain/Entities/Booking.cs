using EventsApi.Domain.Enums;

namespace EventsApi.Domain.Entities
{
    /// <summary>
    /// Бронь на мероприятие.
    /// Жизненный цикл: Pending → Confirmed | Rejected | Cancelled;
    /// подтверждённая бронь также может быть отменена.
    /// </summary>
    public class Booking
    {
        // Приватный конструктор без параметров нужен EF Core: провайдер создаёт
        // экземпляры через рефлексию при чтении данных из БД.
        private Booking() { }

        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public Guid UserId { get; set; }
        public BookingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        /// <summary>Навигационное свойство: событие, к которому относится бронь.</summary>
        public Event? Event { get; set; }

        /// <summary>Пользователь, которому принадлежит бронь.</summary>
        public User? User { get; set; }

        /// <summary>
        /// Создаёт новую бронь в статусе Pending с сгенерированным Id и текущим UTC-временем.
        /// </summary>
        public static Booking CreatePending(Guid eventId, Guid userId) => new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
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

        /// <summary>
        /// Отменяет ожидающую или подтверждённую бронь. Повторная отмена и отмена
        /// уже отклонённой брони запрещены.
        /// </summary>
        public void Cancel(DateTime cancelledAt)
        {
            if (Status == BookingStatus.Cancelled)
                throw new InvalidOperationException("Бронь уже отменена.");

            if (Status == BookingStatus.Rejected)
                throw new InvalidOperationException("Отклонённую бронь нельзя отменить.");

            Status = BookingStatus.Cancelled;
            ProcessedAt = cancelledAt;
        }

        private void EnsurePending()
        {
            if (Status != BookingStatus.Pending)
                throw new InvalidOperationException(
                    $"Переход статуса возможен только из Pending. Текущий статус: {Status}.");
        }
    }
}
