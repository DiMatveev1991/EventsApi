using Users.Application.Dtos;

namespace Users.Application.Services;

/// <summary>Определяет сценарии регистрации и аутентификации.</summary>
public interface IUserService
{
    /// <summary>Регистрирует нового пользователя.</summary>
    Task RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Проверяет учётные данные и возвращает JWT.</summary>
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
