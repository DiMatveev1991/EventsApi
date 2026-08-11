using System.Security.Cryptography;
using System.Text;
using EventsApi.Application.Abstractions;

namespace EventsApi.Infrastructure.Security;

/// <summary>SHA-256 хеширование паролей в соответствии с заданием спринта.</summary>
public sealed class Sha256PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);

        try
        {
            var expected = Convert.FromHexString(passwordHash);
            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
