using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Models;

namespace EventsApi.Services
{
    public class BookingService : IBookingService
    {
        private readonly IBookingStore _bookingStore;
        private readonly IEventService _eventService;

        public BookingService(IBookingStore bookingStore, IEventService eventService)
        {
            _bookingStore = bookingStore;
            _eventService = eventService;
        }

        public Task<BookingDto> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            // Проверяем, что событие существует. IEventService.GetById сам кидает
            // NotFoundException — это нас полностью устраивает: контроллер вернёт 404.
            _ = _eventService.GetById(eventId);

            var booking = Booking.CreatePending(eventId);
            _bookingStore.Add(booking);

            return Task.FromResult(MapToDto(booking));
        }

        public Task<BookingDto> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
        {
            var booking = _bookingStore.GetById(bookingId)
                ?? throw NotFoundException.ForBooking(bookingId);

            return Task.FromResult(MapToDto(booking));
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
