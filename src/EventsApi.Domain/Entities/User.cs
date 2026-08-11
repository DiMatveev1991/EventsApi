using EventsApi.Domain.Enums;

namespace EventsApi.Domain.Entities;

/// <summary>Пользователь API.</summary>
public class User
{
    private User() { }

    public Guid Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    /// <summary>
    /// Создаёт пользователя. Логин хранится в нормализованном нижнем регистре,
    /// чтобы уникальность не зависела от регистра символов.
    /// </summary>
    public static User Create(string login, string passwordHash, UserRole role) => new()
    {
        Id = Guid.NewGuid(),
        Login = login.Trim().ToLowerInvariant(),
        PasswordHash = passwordHash,
        Role = role
    };
}
