using System.ComponentModel.DataAnnotations;
using EventsApi.Domain.Enums;

namespace EventsApi.Application.Dtos;

public sealed class RegisterUserDto
{
    [Required(ErrorMessage = "Поле Login обязательно")]
    [MinLength(3, ErrorMessage = "Login должен содержать не менее 3 символов")]
    [MaxLength(100, ErrorMessage = "Login должен содержать не более 100 символов")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Поле Password обязательно")]
    [MinLength(6, ErrorMessage = "Password должен содержать не менее 6 символов")]
    public string Password { get; set; } = string.Empty;

    [EnumDataType(typeof(UserRole), ErrorMessage = "Неизвестная роль пользователя")]
    public UserRole Role { get; set; } = UserRole.User;
}
