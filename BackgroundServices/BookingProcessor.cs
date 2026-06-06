using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Models;

namespace EventsApi.BackgroundServices
{
	/// <summary>
	/// Фоновый сервис, периодически забирающий из хранилища брони в статусе Pending
	/// и обрабатывающий их параллельно: бронь переводится в Confirmed, либо в Rejected,
	/// если событие к моменту обработки уже удалено или произошла непредвиденная ошибка.
	/// </summary>
	/// <remarks>
	/// Имитирует обращение к внешней системе через <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
	/// Задержки выполняются параллельно (до захвата семафора), а секция
	/// «проверка события + смена статуса + запись в хранилища» защищена
	/// <see cref="SemaphoreSlim"/> — асинхронным аналогом мьютекса. Обычный lock
	/// здесь не подходит: внутри защищаемой секции есть await.
	/// </remarks>
	public class BookingProcessor : BackgroundService
	{
		// Имитация задержки внешнего вызова.
		private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);
		// Период опроса хранилища между итерациями.
		private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(500);

		private readonly IBookingStore _bookingStore;
		private readonly IEventStore _eventStore;
		private readonly ILogger<BookingProcessor> _logger;

		// Защищает запись в хранилища при параллельной обработке броней.
		private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

		public BookingProcessor(
			IBookingStore bookingStore,
			IEventStore eventStore,
			ILogger<BookingProcessor> logger)
		{
			_bookingStore = bookingStore;
			_eventStore = eventStore;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("BookingProcessor запущен");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await ProcessPendingBatchAsync(stoppingToken);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					// штатная остановка
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Ошибка при обработке броней в фоне");
				}

				try
				{
					await Task.Delay(PollingInterval, stoppingToken);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					break;
				}
			}

			_logger.LogInformation("BookingProcessor остановлен");
		}

		private async Task ProcessPendingBatchAsync(CancellationToken stoppingToken)
		{
			var pendingBookings = _bookingStore.GetPending().ToList();
			if (pendingBookings.Count == 0)
				return;

			// Параллельный запуск обработки всех Pending-броней.
			var tasks = pendingBookings.Select(booking => ProcessBookingAsync(booking, stoppingToken));
			await Task.WhenAll(tasks);
		}

		private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
		{
			try
			{
				_logger.LogInformation(
					"Начата обработка брони {BookingId} для события {EventId}",
					booking.Id, booking.EventId);

				// Имитация внешнего вызова ДО захвата семафора —
				// задержки разных броней выполняются параллельно.
				await Task.Delay(ProcessingDelay, stoppingToken);

				// Семафор защищает секцию «проверка события + смена статуса + запись».
				await _processingSemaphore.WaitAsync(stoppingToken);
				try
				{
					// Бронь могла быть обработана ранее (например, повторный запуск).
					if (booking.Status != BookingStatus.Pending)
						return;

					var ev = _eventStore.GetById(booking.EventId);
					if (ev is null)
					{
						booking.Reject(DateTime.UtcNow);
						_bookingStore.Update(booking);
						_logger.LogWarning(
							"Бронь {BookingId} отклонена: событие {EventId} больше не существует",
							booking.Id, booking.EventId);
						return;
					}

					booking.Confirm(DateTime.UtcNow);
					_bookingStore.Update(booking);
					_logger.LogInformation("Бронь {BookingId} подтверждена", booking.Id);
				}
				finally
				{
					_processingSemaphore.Release();
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Штатная остановка: бронь остаётся Pending и будет
				// обработана после перезапуска сервиса.
				_logger.LogInformation(
					"Обработка брони {BookingId} прервана остановкой сервиса", booking.Id);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"Непредвиденная ошибка при обработке брони {BookingId}", booking.Id);
				await RejectAndReleaseSeatAsync(booking);
			}
		}

		/// <summary>
		/// Отклоняет бронь после непредвиденной ошибки и возвращает место в пул события.
		/// Обновляет оба хранилища под семафором.
		/// </summary>
		private async Task RejectAndReleaseSeatAsync(Booking booking)
		{
			await _processingSemaphore.WaitAsync(CancellationToken.None);
			try
			{
				if (booking.Status == BookingStatus.Pending)
				{
					booking.Reject(DateTime.UtcNow);
					_bookingStore.Update(booking);
				}

				var ev = _eventStore.GetById(booking.EventId);
				if (ev is not null)
				{
					ev.ReleaseSeats();
					_eventStore.Update(ev);
				}

				_logger.LogWarning(
					"Бронь {BookingId} отклонена из-за ошибки обработки, место возвращено в пул",
					booking.Id);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Не удалось отклонить бронь {BookingId}", booking.Id);
			}
			finally
			{
				_processingSemaphore.Release();
			}
		}
	}
}