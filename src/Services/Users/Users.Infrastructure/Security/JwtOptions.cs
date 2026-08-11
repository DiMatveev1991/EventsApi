namespace Users.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = "EventsSystem";
    public string Audience { get; init; } = "EventsSystem.Clients";
    public int ExpirationMinutes { get; init; } = 60;
}
