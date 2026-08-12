using Microsoft.EntityFrameworkCore;
using Users.Application.Abstractions;
using Users.Domain.Entities;
using Users.Infrastructure.Persistence;

namespace Users.Infrastructure.Repositories;

/// <summary>Реализует хранилище пользователей поверх EF Core.</summary>
public sealed class UserRepository(UsersDbContext context) : IUserRepository
{
    /// <summary>Возвращает пользователя по нормализованному логину.</summary>
    public Task<User?> GetByLoginAsync(
        string normalizedLogin,
        CancellationToken cancellationToken = default) =>
        context.Users.SingleOrDefaultAsync(
            user => user.Login == normalizedLogin,
            cancellationToken);

    /// <summary>Добавляет пользователя и фиксирует изменения.</summary>
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}
