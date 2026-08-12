namespace Users.Application.Abstractions;

/// <summary>Определяет безопасное хеширование и проверку паролей.</summary>
public interface IPasswordHasher
{
    /// <summary>Создаёт сохраняемый хеш пароля с солью.</summary>
    string Hash(string password);

    /// <summary>Проверяет пароль по ранее сохранённому хешу.</summary>
    bool Verify(string password, string passwordHash);
}
