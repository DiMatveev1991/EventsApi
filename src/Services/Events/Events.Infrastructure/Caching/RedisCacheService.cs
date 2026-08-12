using System.Text.Json;
using EventsApi.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EventsApi.Infrastructure.Caching;

/// <summary>
/// Redis-адаптер кеша. Любой сбой Redis превращается в промах/пропуск записи,
/// поэтому кеш не становится точкой отказа основного сценария.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConnectionMultiplexer connection,
        ILogger<RedisCacheService> logger)
    {
        _database = connection.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await _database.StringGetAsync(key);
            return value.IsNull
                ? null
                : JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Redis read failed for key {CacheKey}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await _database.StringSetAsync(key, payload, timeToLive);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Redis write failed for key {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Redis invalidation failed for key {CacheKey}", key);
        }
    }
}
