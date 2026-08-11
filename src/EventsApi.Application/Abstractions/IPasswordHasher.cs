namespace EventsApi.Application.Abstractions;

/// <summary>Компонент хеширования и проверки паролей.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
