namespace EventsApi.Application.Caching;

/// <summary>Единая точка формирования ключей кеша Events.</summary>
public static class CacheKeys
{
    /// <summary>Ключ кеша рейтинга популярных событий.</summary>
    public const string TopEvents = "events:top10";

    /// <summary>Формирует ключ кеша отдельного события.</summary>
    public static string Event(Guid id) => $"event:{id:D}";
}
