using EventsApi.Application.Dtos;

namespace EventsApi.Application.Services
{
    /// <summary>
    /// Бизнес-логика работы с бронированиями.
    /// </summary>
    public interface IBookingService
    {
        /// <summary>
        /// Создаёт бронь для указанного события в статусе Pending.
        /// </summary>
        /// <exception cref="EventsApi.Domain.Exceptions.NotFoundException">
        /// Событие с указанным идентификатором не существует.
        /// </exception>
        Task<BookingDto> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает текущее состояние брони.
        /// </summary>
        /// <exception cref="EventsApi.Domain.Exceptions.NotFoundException">
        /// Бронь с указанным идентификатором не существует.
        /// </exception>
        Task<BookingDto> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);
    }
}
