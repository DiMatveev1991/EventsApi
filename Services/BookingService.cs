using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsApi.Services
{
	public class BookingService : IBookingService
	{
		private readonly AppDbContext _context;

		// Защищает критическую секцию «проверка доступных мест + создание брони».
		// Сервис — scoped (у каждого запроса свой DbContext), поэтому семафор
		// статический: он синхронизирует все экземпляры между собой. SemaphoreSlim
		// (а не lock) нужен потому, что внутри секции есть await-вызовы.
		private static readonly SemaphoreSlim _bookingSemaphore = new(1, 1);

		public BookingService(AppDbContext context)
		{
			_context = context;
		}

		public async Task<BookingDto> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default)
		{
			// Атомарная пара «проверка + изменение»: без синхронизации два потока могут
			// одновременно увидеть AvailableSeats > 0 и создать брони сверх лимита
			// (овербукинг). Семафор гарантирует, что через секцию проходит один поток.
			await _bookingSemaphore.WaitAsync(cancellationToken);
			try
			{
				// 1. Получение события из БД (внутри секции — чтобы видеть актуальные места).
				var ev = await _context.Events
					.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
					?? throw NotFoundException.ForEvent(eventId);

				// 2. Проверка и резервирование места.
				if (!ev.TryReserveSeats())
					throw new NoAvailableSeatsException();

				// 3. Создание брони.
				var booking = Booking.CreatePending(eventId);
				_context.Bookings.Add(booking);

				// 4. Один вызов SaveChanges сохраняет и новую бронь, и изменение
				// AvailableSeats — оба объекта отслеживаются одним контекстом.
				await _context.SaveChangesAsync(cancellationToken);

				return MapToDto(booking);
			}
			finally
			{
				_bookingSemaphore.Release();
			}
		}

		public async Task<BookingDto> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
		{
			// Операция чтения — синхронизация не нужна.
			var booking = await _context.Bookings
				.AsNoTracking()
				.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
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