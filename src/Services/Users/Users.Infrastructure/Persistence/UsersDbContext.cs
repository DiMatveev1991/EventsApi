using Microsoft.EntityFrameworkCore;
using Users.Domain.Entities;

namespace Users.Infrastructure.Persistence;

/// <summary>Контекст EF Core для базы данных пользователей.</summary>
public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    /// <summary>Набор пользователей.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Подключает конфигурации сущностей текущей сборки.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly);
}
