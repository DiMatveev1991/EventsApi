namespace EventsApi.Application.Caching;

/// <summary>Настраиваемые параметры кеширования сервиса Events.</summary>
public sealed class CacheOptions
{
    public const string SectionName = "Redis";
    public const int DefaultEventTtlSeconds = 300;
    public const int DefaultTopEventsTtlSeconds = 60;

    public int EventTtlSeconds { get; set; } = DefaultEventTtlSeconds;
    public int TopEventsTtlSeconds { get; set; } = DefaultTopEventsTtlSeconds;

    public TimeSpan EventTtl => TimeSpan.FromSeconds(
        EventTtlSeconds > 0 ? EventTtlSeconds : DefaultEventTtlSeconds);

    public TimeSpan TopEventsTtl => TimeSpan.FromSeconds(
        TopEventsTtlSeconds > 0 ? TopEventsTtlSeconds : DefaultTopEventsTtlSeconds);
}
