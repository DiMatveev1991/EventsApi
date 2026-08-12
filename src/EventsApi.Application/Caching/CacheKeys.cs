namespace EventsApi.Application.Caching;

/// <summary>Единая точка формирования ключей кеша Events.</summary>
public static class CacheKeys
{
    public const string TopEvents = "events:top10";

    public static string Event(Guid id) => $"event:{id:D}";
}
