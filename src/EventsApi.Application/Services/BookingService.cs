using EventsApi.Application.Abstractions;
using EventsApi.Application.Dtos;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;

namespace EventsApi.Application.Services
{
    public class BookingService : IBookingService
    {
        public const int MaxActiveBookingsPerUser = 10;

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

        public async Task<BookingDto> CreateBookingAsync(
            Guid eventId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            // Атомарная пара «проверка + изменение» сериализуется только для одного
            // события: бронирования разных событий выполняются параллельно.
            using var bookingLock = await _bookingLock.AcquireAsync(eventId, cancellationToken);

            var ev = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
                ?? throw NotFoundException.ForEvent(eventId);

            if (ev.StartAt <= DateTimeOffset.UtcNow)
                throw new EventAlreadyStartedException();

            var activeBookings = await _bookingRepository.CountActiveByUserIdAsync(
                userId,
                cancellationToken);
            if (activeBookings >= MaxActiveBookingsPerUser)
                throw new ActiveBookingLimitExceededException(MaxActiveBookingsPerUser);

            if (!ev.TryReserveSeats())
                throw new NoAvailableSeatsException();

            // Репозитории event и booking делят один scoped AppDbContext, поэтому
            // сохранение брони фиксирует и изменение AvailableSeats одной транзакцией.
            var booking = Booking.CreatePending(eventId, userId);
            await _bookingRepository.AddAsync(booking, cancellationToken);

            return MapToDto(booking);
        }

        public async Task CancelBookingAsync(
            Guid bookingId,
            Guid currentUserId,
            bool isAdmin,
            CancellationToken cancellationToken = default)
        {
            var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken)
                ?? throw NotFoundException.ForBooking(bookingId);

            if (!isAdmin && booking.UserId != currentUserId)
                throw new ForbiddenException("Можно отменять только собственные бронирования");

            using var bookingLock = await _bookingLock.AcquireAsync(booking.EventId, cancellationToken);

            try
            {
                booking.Cancel(DateTime.UtcNow);
            }
            catch (InvalidOperationException exception)
            {
                throw new ValidationException(exception.Message);
            }

            var ev = await _eventRepository.GetByIdAsync(booking.EventId, cancellationToken);
            ev?.ReleaseSeats();

            await _bookingRepository.UpdateAsync(booking, cancellationToken);
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
            UserId = booking.UserId,
            Status = booking.Status,
            CreatedAt = booking.CreatedAt,
            ProcessedAt = booking.ProcessedAt
        };
    }
}
