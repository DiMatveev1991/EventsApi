using EventsApi.Application.Abstractions;
using EventsApi.Application.Dtos;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;

namespace EventsApi.Application.Services
{
    public class BookingService : IBookingService
    {
        private readonly IEventRepository _eventRepository;
        private readonly IBookingRepository _bookingRepository;
        private readonly IEventBookingLock _bookingLock;

        public BookingService(
            IEventRepository eventRepository,
            IBookingRepository bookingRepository,
            IEventBookingLock bookingLock)
        {
            _eventRepository = eventRepository;
            _bookingRepository = bookingRepository;
            _bookingLock = bookingLock;
        }

        public async Task<BookingDto> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            // Атомарная пара «проверка + изменение» сериализуется только для одного
            // события: бронирования разных событий выполняются параллельно.
            using var bookingLock = await _bookingLock.AcquireAsync(eventId, cancellationToken);

            var ev = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
                ?? throw NotFoundException.ForEvent(eventId);

            if (!ev.TryReserveSeats())
                throw new NoAvailableSeatsException();

            // Репозитории event и booking делят один scoped AppDbContext, поэтому
            // сохранение брони фиксирует и изменение AvailableSeats одной транзакцией.
            var booking = Booking.CreatePending(eventId);
            await _bookingRepository.AddAsync(booking, cancellationToken);

            return MapToDto(booking);
        }

        public async Task<BookingDto> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
        {
            // Операция чтения — синхронизация не нужна.
            var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken)
                ?? throw NotFoundException.ForBooking(bookingId);

            return MapToDto(booking);
        }

        internal static BookingDto MapToDto(Booking booking) => new()
        {
            Id = booking.Id,
            EventId = booking.EventId,
            Status = booking.Status,
            CreatedAt = booking.CreatedAt,
            ProcessedAt = booking.ProcessedAt
        };
    }
}
