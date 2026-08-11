using Users.Domain.Enums;

namespace Users.Domain.Entities;

public sealed class User
{
    private User() { }

    public Guid Id { get; private set; }
    public string Login { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }

    public static User Create(string login, string passwordHash, UserRole role) => new()
    {
        Id = Guid.NewGuid(),
        Login = login.Trim().ToLowerInvariant(),
        PasswordHash = passwordHash,
        Role = role
    };
}
