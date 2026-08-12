using System.ComponentModel.DataAnnotations;

namespace Users.Application.Dtos;

/// <summary>Данные запроса на аутентификацию.</summary>
public sealed class LoginRequest
{
    /// <summary>Логин пользователя.</summary>
    [Required]
    public string Login { get; init; } = string.Empty;

    /// <summary>Пароль пользователя.</summary>
    [Required]
    public string Password { get; init; } = string.Empty;
}
