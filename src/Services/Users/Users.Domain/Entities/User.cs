using Users.Domain.Enums;

namespace Users.Domain.Entities;

/// <summary>Доменная сущность пользователя системы.</summary>
public sealed class User
{
    /// <summary>Создаёт пустой экземпляр для материализации EF Core.</summary>
    private User() { }

    /// <summary>Идентификатор пользователя.</summary>
    public Guid Id { get; private set; }

    /// <summary>Нормализованный уникальный логин.</summary>
    public string Login { get; private set; } = string.Empty;

    /// <summary>PBKDF2-хеш пароля с солью и числом итераций.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Роль пользователя.</summary>
    public UserRole Role { get; private set; }

    /// <summary>Создаёт пользователя с новым идентификатором.</summary>
    public static User Create(string login, string passwordHash, UserRole role) => new()
    {
        Id = Guid.NewGuid(),
        Login = login.Trim().ToLowerInvariant(),
        PasswordHash = passwordHash,
        Role = role
    };
}
