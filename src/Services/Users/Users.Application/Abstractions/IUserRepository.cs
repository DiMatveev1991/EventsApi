using Users.Domain.Entities;

namespace Users.Application.Abstractions;

/// <summary>Определяет операции хранилища пользователей.</summary>
public interface IUserRepository
{
    /// <summary>Возвращает пользователя по нормализованному логину.</summary>
    Task<User?> GetByLoginAsync(string normalizedLogin, CancellationToken cancellationToken = default);

    /// <summary>Добавляет пользователя и фиксирует изменения.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
