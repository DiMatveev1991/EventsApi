namespace EventsApi.Application.Abstractions;

/// <summary>
/// Технологически независимый порт кеша. Реализация и формат хранения находятся
/// в Infrastructure, поэтому Application не зависит от Redis.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
        where T : class;

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where T : class;

    Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default);
}
