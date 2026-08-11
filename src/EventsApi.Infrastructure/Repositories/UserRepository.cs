using EventsApi.Application.Abstractions;
using EventsApi.Domain.Entities;
using EventsApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventsApi.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByLoginAsync(
        string normalizedLogin,
        CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(
            user => user.Login == normalizedLogin,
            cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
