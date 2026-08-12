using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Users.Application.Abstractions;
using Users.Domain.Entities;

namespace Users.Infrastructure.Security;

/// <summary>Выпускает подписанные JWT для общей авторизации сервисов.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    /// <summary>Создаёт JWT с идентификатором, именем и ролью пользователя.</summary>
    public string CreateToken(User user)
    {
        var jwt = options.Value;
        if (Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
            throw new InvalidOperationException("Jwt:Secret must contain at least 32 bytes.");

        var claims = new[]
        {
            // sub соответствует стандарту JWT, а NameIdentifier нужен стандартной
            // модели claims ASP.NET Core и контроллерам остальных сервисов.
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            jwt.Issuer,
            jwt.Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(jwt.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
