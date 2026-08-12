using EventsApi.Application.Abstractions;
using EventsApi.Infrastructure.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace EventsApi.Tests;

public sealed class CacheRegistrationTests
{
    [Fact]
    public void Redis_connection_and_cache_are_registered_as_singletons()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EventsDatabase"] =
                    "Host=localhost;Database=events;Username=postgres;Password=postgres",
                ["Redis:ConnectionString"] = "localhost:6379"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IConnectionMultiplexer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ICacheService) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
