using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Services;

namespace EventsApi.BackgroundServices
{
    /// <summary>
    /// Фоновый сервис, периодически забирающий из хранилища брони в статусе Pending
    /// и переводящий их в Confirmed (или Rejected, если событие к моменту обработки
    /// уже удалено).
    /// </summary>
    /// <remarks>
    /// Имитирует обращение к внешней системе через <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// Корректно реагирует на отмену через <see cref="CancellationToken"/>.
    /// </remarks>
    public class BookingProcessor : BackgroundService
    {
        // Имитация задержки внешнего вызова. Дефолт по ТЗ — 2 секунды.
        private static readonly TimeSpan ExternalCallDelay = TimeSpan.FromSeconds(2);
        // Период опроса хранилища между итерациями (когда Pending пуст).
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BookingProcessor> _logger;

        public BookingProcessor(IServiceScopeFactory scopeFactory, ILogger<BookingProcessor> logger)
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
                    _logger.LogError(ex, "Ошибка при обработке брони в фоне");
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("BookingProcessor остановлен");
        }

        private async Task ProcessPendingBatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IBookingStore>();
            var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();

            var pending = store.GetPending();
            if (pending.Count == 0)
                return;

            foreach (var booking in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation(
                    "Обработка брони {BookingId} для события {EventId}",
                    booking.Id, booking.EventId);

                // Имитация обращения к внешней системе.
                await Task.Delay(ExternalCallDelay, cancellationToken);

                // Если бронь уже была кем-то обработана (например, повторный запуск),
                // пропускаем её.
                if (booking.Status != BookingStatus.Pending)
                    continue;

                if (EventStillExists(eventService, booking.EventId))
                {
                    booking.Confirm(DateTime.UtcNow);
                    _logger.LogInformation("Бронь {BookingId} подтверждена", booking.Id);
                }
                else
                {
                    booking.Reject(DateTime.UtcNow);
                    _logger.LogWarning(
                        "Бронь {BookingId} отклонена: событие {EventId} больше не существует",
                        booking.Id, booking.EventId);
                }

                store.Update(booking);
            }
        }

        private static bool EventStillExists(IEventService eventService, Guid eventId)
        {
            try
            {
                _ = eventService.GetById(eventId);
                return true;
            }
            catch (NotFoundException)
            {
                return false;
            }
        }
    }
}
