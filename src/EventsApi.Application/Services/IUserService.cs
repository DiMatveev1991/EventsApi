using EventsApi.Application.Dtos;

namespace EventsApi.Application.Services;

public interface IUserService
{
    Task RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default);
    Task<TokenDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
}
