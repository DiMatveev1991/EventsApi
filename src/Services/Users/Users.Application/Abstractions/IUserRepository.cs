using Users.Domain.Entities;

namespace Users.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByLoginAsync(string normalizedLogin, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
