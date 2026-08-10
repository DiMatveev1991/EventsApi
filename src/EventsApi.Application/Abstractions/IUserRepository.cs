using EventsApi.Domain.Entities;

namespace EventsApi.Application.Abstractions;

/// <summary>Порт доступа к пользователям.</summary>
public interface IUserRepository
{
    Task<User?> GetByLoginAsync(string normalizedLogin, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
