using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Models;
using EventsApi.Repositories;

namespace EventsApi.Services
{
	public class BookingService : IBookingService
	{
		private readonly IEventRepository _eventRepository;
		private readonly IBookingRepository _bookingRepository;

		// Защищает критическую секцию «проверка доступных мест + создание брони».
		// Сервис — scoped (у каждого запроса свой DbContext), поэтому семафор
		// статический: он синхронизирует все экземпляры между собой. SemaphoreSlim
		// (а не lock) нужен потому, что внутри секции есть await-вызовы.
		private static readonly SemaphoreSlim _bookingSemaphore = new(1, 1);

		public BookingService(IEventRepository eventRepository, IBookingRepository bookingRepository)
		{
			_eventRepository = eventRepository;
			_bookingRepository = bookingRepository;
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
				var ev = await _eventRepository.GetByIdAsync(eventId, cancellationToken)
					?? throw NotFoundException.ForEvent(eventId);

				// 2. Проверка и резервирование места.
				if (!ev.TryReserveSeats())
					throw new NoAvailableSeatsException();

				// 3. Создание брони. Репозитории event и booking делят один scoped
				// AppDbContext, поэтому сохранение брони одной транзакцией фиксирует
				// и изменение AvailableSeats у отслеживаемого события.
				var booking = Booking.CreatePending(eventId);
				await _bookingRepository.AddAsync(booking, cancellationToken);

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