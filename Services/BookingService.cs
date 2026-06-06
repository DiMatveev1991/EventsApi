using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Models;

namespace EventsApi.Services
{
	public class BookingService : IBookingService
	{
		private readonly IBookingStore _bookingStore;
		private readonly IEventStore _eventStore;

		// Защищает критическую секцию «проверка доступных мест + создание брони».
		private readonly object _bookingLock = new();

		public BookingService(IBookingStore bookingStore, IEventStore eventStore)
		{
			_bookingStore = bookingStore;
			_eventStore = eventStore;
		}

		public Task<BookingDto> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default)
		{
			// Атомарная пара «проверка + изменение»: без lock два потока могут
			// одновременно увидеть AvailableSeats > 0 и создать брони сверх лимита
			// (овербукинг). lock гарантирует, что через секцию проходит один поток.
			lock (_bookingLock)
			{
				// 1. Получение события из хранилища.
				var ev = _eventStore.GetById(eventId)
					?? throw NotFoundException.ForEvent(eventId);

				// 2. Проверка доступных мест.
				if (!ev.TryReserveSeats())
					throw new NoAvailableSeatsException();

				// 3. Сохранение обновлённого события.
				_eventStore.Update(ev);

				// 4. Создание и сохранение брони.
				var booking = Booking.CreatePending(eventId);
				_bookingStore.Add(booking);

				return Task.FromResult(MapToDto(booking));
			}
		}

		public Task<BookingDto> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
		{
			// Операция чтения — блокировка не нужна, lock охватывает только
			// минимально необходимую критическую секцию в CreateBookingAsync.
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