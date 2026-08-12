using EventsApi.Application.Abstractions;
using EventsApi.Application.Caching;
using EventsApi.Application.Services;
using EventsApi.Infrastructure.Persistence;
using EventsApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventsApi.Tests;

internal static class EventTestHost
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddSingleton<ICacheService, InMemoryTestCache>();
        services.AddSingleton<IOptions<CacheOptions>>(
            Options.Create(new CacheOptions()));
        services.AddScoped<IEventService, EventService>();
        return services.BuildServiceProvider();
    }

    private sealed class InMemoryTestCache : ICacheService
    {
        private readonly Dictionary<string, object> _values = new();

        public Task<T?> GetAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
            where T : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                _values.TryGetValue(key, out var value) ? value as T : null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default)
            where T : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
