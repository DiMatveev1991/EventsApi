using EventsApi.DTOs;

namespace EventsApi.Services
{
    /// <summary>
    /// Бизнес-логика работы с бронированиями.
    /// </summary>
    public interface IBookingService
    {
        /// <summary>
        /// Создаёт бронь для указанного события в статусе Pending.
        /// </summary>
        /// <exception cref="EventsApi.Exceptions.NotFoundException">
        /// Событие с указанным идентификатором не существует.
        /// </exception>
        Task<BookingDto> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает текущее состояние брони.
        /// </summary>
        /// <exception cref="EventsApi.Exceptions.NotFoundException">
        /// Бронь с указанным идентификатором не существует.
        /// </exception>
        Task<BookingDto> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);
    }
}
