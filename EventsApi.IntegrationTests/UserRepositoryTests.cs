using EventsApi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Users.Domain.Entities;
using Users.Domain.Enums;
using Users.Infrastructure.Repositories;
using Xunit;

namespace EventsApi.IntegrationTests;

public sealed class UserRepositoryTests
{
    [Fact]
    public async Task Add_and_lookup_persist_user()
    {
        await using var database = await SqliteTestDatabase.CreateUsersAsync();
        var repository = new UserRepository(database.Context);
        var user = User.Create("  Dmitry  ", "hash", UserRole.Admin);

        await repository.AddAsync(user);
        database.Context.ChangeTracker.Clear();
        var stored = await repository.GetByLoginAsync("dmitry");

        stored.Should().NotBeNull();
        stored!.Id.Should().Be(user.Id);
        stored.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task Lookup_unknown_login_returns_null()
    {
        await using var database = await SqliteTestDatabase.CreateUsersAsync();
        var repository = new UserRepository(database.Context);

        var stored = await repository.GetByLoginAsync("missing");

        stored.Should().BeNull();
    }

    [Fact]
    public async Task Unique_index_rejects_duplicate_normalized_login()
    {
        await using var database = await SqliteTestDatabase.CreateUsersAsync();
        var repository = new UserRepository(database.Context);
        await repository.AddAsync(User.Create("user", "hash-one", UserRole.User));

        var action = () => repository.AddAsync(
            User.Create("USER", "hash-two", UserRole.User));

        await action.Should().ThrowAsync<DbUpdateException>();
    }
}
