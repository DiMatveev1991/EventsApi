using Users.Domain.Entities;

namespace Users.Application.Abstractions;

/// <summary>Определяет выпуск токенов доступа пользователя.</summary>
public interface ITokenService
{
    /// <summary>Создаёт подписанный токен для пользователя.</summary>
    string CreateToken(User user);
}
