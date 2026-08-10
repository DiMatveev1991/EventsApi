using System.ComponentModel.DataAnnotations;

namespace EventsApi.Application.Dtos;

public sealed class LoginDto
{
    [Required(ErrorMessage = "Поле Login обязательно")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Поле Password обязательно")]
    public string Password { get; set; } = string.Empty;
}
