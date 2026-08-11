using System.ComponentModel.DataAnnotations;
using Users.Domain.Enums;

namespace Users.Application.Dtos;

public sealed class RegisterUserRequest
{
    [Required, MinLength(3), MaxLength(100)]
    public string Login { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; init; } = UserRole.User;
}
