namespace Users.Application.Dtos;

/// <summary>Ответ с выпущенным JWT access token.</summary>
/// <param name="Token">Подписанный JWT.</param>
public sealed record TokenResponse(string Token);
