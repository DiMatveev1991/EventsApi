using Users.Application.Abstractions;
using Users.Application.Dtos;
using Users.Domain.Entities;
using Users.Domain.Exceptions;

namespace Users.Application.Services;

/// <summary>Реализует регистрацию и аутентификацию пользователей.</summary>
public sealed class UserService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IUserService
{
    /// <summary>Создаёт пользователя с нормализованным логином и хешем пароля.</summary>
    public async Task RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var login = NormalizeLogin(request.Login);
        if (await users.GetByLoginAsync(login, cancellationToken) is not null)
            throw new ValidationException("Пользователь с таким логином уже существует");

        var user = User.Create(login, passwordHasher.Hash(request.Password), request.Role);
        await users.AddAsync(user, cancellationToken);
    }

    /// <summary>Проверяет учётные данные и выпускает JWT.</summary>
    public async Task<TokenResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetByLoginAsync(NormalizeLogin(request.Login), cancellationToken);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new ValidationException("Неверный логин или пароль");

        return new TokenResponse(tokenService.CreateToken(user));
    }

    /// <summary>Приводит логин к единому регистронезависимому формату.</summary>
    private static string NormalizeLogin(string login) => login.Trim().ToLowerInvariant();
}
