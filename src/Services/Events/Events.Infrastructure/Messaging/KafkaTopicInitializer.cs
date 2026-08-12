using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventsApi.Infrastructure.Messaging;

/// <summary>Создаёт обязательные Kafka-топики до запуска основного потока сообщений.</summary>
public sealed class KafkaTopicInitializer(
    IConfiguration configuration,
    ILogger<KafkaTopicInitializer> logger) : IHostedService
{
    /// <summary>Создаёт топик подтверждений с повторами на случай старта Kafka.</summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");

        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers
        }).Build();

        // Kafka и API стартуют параллельно в compose, поэтому инициализация
        // допускает временную недоступность брокера и выполняет ограниченные повторы.
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await admin.CreateTopicsAsync(new[]
                {
                    new TopicSpecification
                    {
                        Name = KafkaTopics.BookingConfirmed,
                        NumPartitions = 3,
                        ReplicationFactor = 1
                    }
                });
                logger.LogInformation("Kafka topic {Topic} created", KafkaTopics.BookingConfirmed);
                return;
            }
            catch (CreateTopicsException exception)
                when (exception.Results.All(result =>
                    result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                logger.LogInformation(
                    "Kafka topic {Topic} already exists",
                    KafkaTopics.BookingConfirmed);
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Kafka topic initialization attempt {Attempt} failed",
                    attempt);
                if (attempt < 10)
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        logger.LogError(
            "Kafka topic {Topic} could not be created; consumer will keep retrying",
            KafkaTopics.BookingConfirmed);
    }

    /// <summary>Завершает инициализатор; постоянных ресурсов после старта у него нет.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
