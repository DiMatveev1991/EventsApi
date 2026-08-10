using EventsApi.Domain.Entities;
using EventsApi.Domain.Enums;
using EventsApi.Infrastructure.Repositories;
using EventsApi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventsApi.IntegrationTests;

public sealed class UserRepositoryTests : IntegrationTestBase
{
    public UserRepositoryTests(PostgresDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AddAndGetByLogin_PersistsUser()
    {
        var user = User.Create("Dmitry", new string('A', 64), UserRole.Admin);

        await using (var context = CreateContext())
            await new UserRepository(context).AddAsync(user);

        await using var assertContext = CreateContext();
        var stored = await new UserRepository(assertContext).GetByLoginAsync("dmitry");

        stored.Should().NotBeNull();
        stored!.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task AddAsync_DuplicateNormalizedLogin_ViolatesUniqueIndex()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        await repository.AddAsync(User.Create("dmitry", new string('A', 64), UserRole.User));

        var act = async () => await repository.AddAsync(
            User.Create("DMITRY", new string('B', 64), UserRole.User));

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
