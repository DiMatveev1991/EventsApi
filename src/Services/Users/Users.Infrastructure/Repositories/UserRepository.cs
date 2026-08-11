using Microsoft.EntityFrameworkCore;
using Users.Application.Abstractions;
using Users.Domain.Entities;
using Users.Infrastructure.Persistence;

namespace Users.Infrastructure.Repositories;

public sealed class UserRepository(UsersDbContext context) : IUserRepository
{
    public Task<User?> GetByLoginAsync(
        string normalizedLogin,
        CancellationToken cancellationToken = default) =>
        context.Users.SingleOrDefaultAsync(
            user => user.Login == normalizedLogin,
            cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}
