namespace EventsApi.Infrastructure.Security;

/// <summary>Настройки выпуска и проверки JWT.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "EventsApi";
    public string Audience { get; set; } = "EventsApi.Client";
    public string Secret { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 60;
}
