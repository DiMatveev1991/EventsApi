using System.ComponentModel.DataAnnotations;
using Users.Domain.Enums;

namespace Users.Application.Dtos;

/// <summary>Данные запроса на регистрацию пользователя.</summary>
public sealed class RegisterUserRequest
{
    /// <summary>Уникальный логин пользователя.</summary>
    [Required, MinLength(3), MaxLength(100)]
    public string Login { get; init; } = string.Empty;

    /// <summary>Пароль пользователя длиной не менее восьми символов.</summary>
    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    /// <summary>Назначаемая пользователю роль.</summary>
    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; init; } = UserRole.User;
}
