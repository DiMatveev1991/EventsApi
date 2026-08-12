using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bookings.Infrastructure.Messaging;

public sealed class KafkaHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"]
            }).Build();
            var metadata = admin.GetMetadata(TimeSpan.FromSeconds(3));
            return Task.FromResult(metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy("Kafka broker is available.")
                : HealthCheckResult.Unhealthy("Kafka returned no brokers."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Kafka broker is unavailable.",
                exception));
        }
    }
}
