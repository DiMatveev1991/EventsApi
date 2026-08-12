namespace EventsApi.Application.Abstractions;

/// <summary>
/// Технологически независимый порт кеша. Реализация и формат хранения находятся
/// в Infrastructure, поэтому Application не зависит от Redis.
/// </summary>
public interface ICacheService
{
    /// <summary>Получает и десериализует значение из кеша либо возвращает <c>null</c>.</summary>
    Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Сериализует значение и сохраняет его в кеше на заданное время.</summary>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Удаляет значение из кеша по ключу.</summary>
    Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default);
}
