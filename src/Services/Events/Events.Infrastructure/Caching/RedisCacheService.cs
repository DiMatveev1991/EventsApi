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

    /// <summary>Создаёт адаптер поверх общего подключения StackExchange.Redis.</summary>
    public RedisCacheService(
        IConnectionMultiplexer connection,
        ILogger<RedisCacheService> logger)
    {
        _database = connection.GetDatabase();
        _logger = logger;
    }

    /// <summary>Возвращает значение из Redis либо <c>null</c> при промахе или сбое.</summary>
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
            // Кеш — необязательная оптимизация: ошибка чтения эквивалентна промаху.
            _logger.LogWarning(exception, "Redis read failed for key {CacheKey}", key);
            return null;
        }
    }

    /// <summary>Сохраняет сериализованное значение в Redis с указанным TTL.</summary>
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
            // Сбой Redis не должен отменять уже успешно выполненную операцию в БД.
            _logger.LogWarning(exception, "Redis write failed for key {CacheKey}", key);
        }
    }

    /// <summary>Удаляет ключ из Redis, не прерывая основной сценарий при сбое кеша.</summary>
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
