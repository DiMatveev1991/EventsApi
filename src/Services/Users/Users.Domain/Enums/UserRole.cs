namespace Users.Domain.Enums;

/// <summary>Роль пользователя в общей JWT-модели сервисов.</summary>
public enum UserRole
{
    /// <summary>Обычный пользователь.</summary>
    User = 0,

    /// <summary>Администратор с доступом к защищённым операциям.</summary>
    Admin = 1
}
