using Users.Application.Dtos;

namespace Users.Application.Services;

public interface IUserService
{
    Task RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
