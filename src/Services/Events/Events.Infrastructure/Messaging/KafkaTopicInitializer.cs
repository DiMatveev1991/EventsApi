using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventsApi.Infrastructure.Messaging;

/// <summary>Creates required topics before the consumer hosted service starts.</summary>
public sealed class KafkaTopicInitializer(
    IConfiguration configuration,
    ILogger<KafkaTopicInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");

        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers
        }).Build();

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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
