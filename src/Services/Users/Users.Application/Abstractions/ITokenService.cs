using Users.Domain.Entities;

namespace Users.Application.Abstractions;

public interface ITokenService
{
    string CreateToken(User user);
}
