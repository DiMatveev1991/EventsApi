namespace EventsApi.Application.Caching;

/// <summary>Настраиваемые параметры кеширования сервиса Events.</summary>
public sealed class CacheOptions
{
    /// <summary>Имя секции конфигурации Redis.</summary>
    public const string SectionName = "Redis";

    /// <summary>TTL карточки события по умолчанию, в секундах.</summary>
    public const int DefaultEventTtlSeconds = 300;

    /// <summary>TTL рейтинга популярных событий по умолчанию, в секундах.</summary>
    public const int DefaultTopEventsTtlSeconds = 60;

    /// <summary>TTL карточки события, в секундах.</summary>
    public int EventTtlSeconds { get; set; } = DefaultEventTtlSeconds;

    /// <summary>TTL рейтинга популярных событий, в секундах.</summary>
    public int TopEventsTtlSeconds { get; set; } = DefaultTopEventsTtlSeconds;

    /// <summary>Возвращает корректный TTL карточки события.</summary>
    public TimeSpan EventTtl => TimeSpan.FromSeconds(
        EventTtlSeconds > 0 ? EventTtlSeconds : DefaultEventTtlSeconds);

    /// <summary>Возвращает корректный TTL рейтинга популярных событий.</summary>
    public TimeSpan TopEventsTtl => TimeSpan.FromSeconds(
        TopEventsTtlSeconds > 0 ? TopEventsTtlSeconds : DefaultTopEventsTtlSeconds);
}
