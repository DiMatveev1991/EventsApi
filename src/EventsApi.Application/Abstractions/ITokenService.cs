using EventsApi.Domain.Entities;

namespace EventsApi.Application.Abstractions;

/// <summary>Компонент выпуска токенов доступа.</summary>
public interface ITokenService
{
    string CreateToken(User user);
}
