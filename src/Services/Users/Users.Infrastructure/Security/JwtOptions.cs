namespace Users.Infrastructure.Security;

/// <summary>Настройки выпуска и проверки JWT.</summary>
public sealed class JwtOptions
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Секрет симметричной подписи.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Издатель токена.</summary>
    public string Issuer { get; init; } = "EventsSystem";

    /// <summary>Допустимая аудитория токена.</summary>
    public string Audience { get; init; } = "EventsSystem.Clients";

    /// <summary>Время жизни токена в минутах.</summary>
    public int ExpirationMinutes { get; init; } = 60;
}
