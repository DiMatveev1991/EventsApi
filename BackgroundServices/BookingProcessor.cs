using EventsApi.DataAccess;
using EventsApi.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EventsApi.BackgroundServices
{
	/// <summary>
	/// Фоновый сервис, периодически забирающий из БД брони в статусе Pending
	/// и обрабатывающий их параллельно: бронь переводится в Confirmed, либо в Rejected,
	/// если событие к моменту обработки уже удалено или произошла непредвиденная ошибка.
	/// </summary>
	/// <remarks>
	/// BackgroundService — синглтон, а <see cref="AppDbContext"/> — scoped, поэтому
	/// зависимости получаем через <see cref="IServiceScopeFactory"/>: для каждой брони
	/// создаётся свой scope со своим DbContext. Так как контексты не разделяются между
	/// задачами, дополнительная синхронизация (семафор) не нужна — изоляцию обеспечивает
	/// отдельный экземпляр контекста на каждую задачу.
	/// </remarks>
	public class BookingProcessor : BackgroundService
	{
		// Имитация задержки внешнего вызова.
		private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);
		// Период опроса БД между итерациями.
		private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(500);

		private readonly IServiceScopeFactory _scopeFactory;
		private readonly ILogger<BookingProcessor> _logger;

		public BookingProcessor(
			IServiceScopeFactory scopeFactory,
			ILogger<BookingProcessor> logger)
		{
			_scopeFactory = scopeFactory;
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
			// Отдельный scope для чтения идентификаторов Pending-броней; закрывается сразу.
			List<Guid> pendingIds;
			using (var scope = _scopeFactory.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
				pendingIds = await db.Bookings
					.Where(b => b.Status == BookingStatus.Pending)
					.Select(b => b.Id)
					.ToListAsync(stoppingToken);
			}

			if (pendingIds.Count == 0)
				return;

			// Параллельный запуск обработки всех Pending-броней — у каждой свой scope.
			var tasks = pendingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
			await Task.WhenAll(tasks);
		}

		private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
		{
			try
			{
				_logger.LogInformation("Начата обработка брони {BookingId}", bookingId);

				// Имитация внешнего вызова ДО создания scope —
				// задержки разных броней выполняются параллельно.
				await Task.Delay(ProcessingDelay, stoppingToken);

				using var scope = _scopeFactory.CreateScope();
				var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

				var booking = await db.Bookings
					.FirstOrDefaultAsync(b => b.Id == bookingId, stoppingToken);

				// Бронь могла быть удалена или уже обработана ранее.
				if (booking is null || booking.Status != BookingStatus.Pending)
					return;

				var ev = await db.Events
					.FirstOrDefaultAsync(e => e.Id == booking.EventId, stoppingToken);

				if (ev is null)
				{
					booking.Reject(DateTime.UtcNow);
					await db.SaveChangesAsync(stoppingToken);
					_logger.LogWarning(
						"Бронь {BookingId} отклонена: событие {EventId} больше не существует",
						booking.Id, booking.EventId);
					return;
				}

				booking.Confirm(DateTime.UtcNow);
				await db.SaveChangesAsync(stoppingToken);
				_logger.LogInformation("Бронь {BookingId} подтверждена", booking.Id);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Штатная остановка: бронь остаётся Pending и будет
				// обработана после перезапуска сервиса.
				_logger.LogInformation(
					"Обработка брони {BookingId} прервана остановкой сервиса", bookingId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"Непредвиденная ошибка при обработке брони {BookingId}", bookingId);
				await RejectAndReleaseSeatAsync(bookingId);
			}
		}

		/// <summary>
		/// Отклоняет бронь после непредвиденной ошибки и возвращает место в пул события.
		/// Работает в собственном scope; и бронь, и событие сохраняются одним SaveChanges.
		/// </summary>
		private async Task RejectAndReleaseSeatAsync(Guid bookingId)
		{
			try
			{
				using var scope = _scopeFactory.CreateScope();
				var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

				var booking = await db.Bookings
					.FirstOrDefaultAsync(b => b.Id == bookingId);
				if (booking is null)
					return;

				if (booking.Status == BookingStatus.Pending)
					booking.Reject(DateTime.UtcNow);

				var ev = await db.Events
					.FirstOrDefaultAsync(e => e.Id == booking.EventId);
				ev?.ReleaseSeats();

				await db.SaveChangesAsync();

				_logger.LogWarning(
					"Бронь {BookingId} отклонена из-за ошибки обработки, место возвращено в пул",
					bookingId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Не удалось отклонить бронь {BookingId}", bookingId);
			}
		}
	}
}