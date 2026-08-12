using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Users.Application.Abstractions;
using Users.Domain.Entities;

namespace Users.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public string CreateToken(User user)
    {
        var jwt = options.Value;
        if (Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
            throw new InvalidOperationException("Jwt:Secret must contain at least 32 bytes.");

        var claims = new[]
        {
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
