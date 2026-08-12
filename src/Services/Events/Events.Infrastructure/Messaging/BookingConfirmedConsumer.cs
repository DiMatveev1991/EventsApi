using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using EventsApi.Application.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventsApi.Infrastructure.Messaging;

/// <summary>
/// Читает подтверждения бронирований из Kafka и передаёт их scoped-обработчику.
/// </summary>
public sealed class BookingConfirmedConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingConfirmedConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;

    /// <summary>Создаёт Kafka consumer с ручным подтверждением смещения.</summary>
    public BookingConfirmedConsumer(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingConfirmedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = configuration["Kafka:ConsumerGroup"] ?? "events-service",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();
    }

    /// <summary>Запускает цикл чтения Kafka до остановки приложения.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(KafkaTopics.BookingConfirmed);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string> consumed;
                try
                {
                    consumed = await Task.Run(
                        () => _consumer.Consume(stoppingToken),
                        stoppingToken);
                }
                catch (ConsumeException exception)
                {
                    _logger.LogError(exception, "Kafka consume failed");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                try
                {
                    var message = JsonSerializer.Deserialize<BookingConfirmed>(
                        consumed.Message.Value,
                        JsonOptions);
                    if (message is null)
                    {
                        _logger.LogWarning("Empty BookingConfirmed message skipped");
                        _consumer.Commit(consumed);
                        continue;
                    }

                    // BackgroundService — singleton, поэтому scoped-репозиторий и
                    // DbContext разрешаются только через отдельный scope сообщения.
                    using var scope = _scopeFactory.CreateScope();
                    var handler = scope.ServiceProvider
                        .GetRequiredService<IBookingConfirmedHandler>();
                    var result = await handler.HandleAsync(message, stoppingToken);

                    LogResult(message, result);
                    // Offset фиксируется только после успешной обработки. При сбое
                    // сообщение будет доставлено повторно и остановлено inbox-проверкой.
                    _consumer.Commit(consumed);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning(exception, "Malformed Kafka message skipped");
                    _consumer.Commit(consumed);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogError(
                        exception,
                        "BookingConfirmed processing failed; offset will not be committed");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        finally
        {
            _consumer.Close();
            _consumer.Dispose();
        }
    }

    /// <summary>Записывает результат обработки сообщения с подходящим уровнем журнала.</summary>
    private void LogResult(
        BookingConfirmed message,
        BookingConfirmationResult result)
    {
        switch (result)
        {
            case BookingConfirmationResult.Applied:
                _logger.LogInformation(
                    "Reserved {Seats} seats for event {EventId} from booking {BookingId}",
                    message.Seats,
                    message.EventId,
                    message.BookingId);
                break;
            case BookingConfirmationResult.Duplicate:
                _logger.LogInformation(
                    "Duplicate booking {BookingId} ignored",
                    message.BookingId);
                break;
            case BookingConfirmationResult.EventNotFound:
                _logger.LogWarning(
                    "Event {EventId} not found for booking {BookingId}; message skipped",
                    message.EventId,
                    message.BookingId);
                break;
            case BookingConfirmationResult.InsufficientSeats:
                _logger.LogWarning(
                    "Not enough seats for booking {BookingId}; message skipped",
                    message.BookingId);
                break;
        }
    }
}
