using Bookings.Infrastructure.Persistence;
using EventsApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Users.Infrastructure.Persistence;
using Xunit;

namespace EventsApi.IntegrationTests.Infrastructure;

[CollectionDefinition("PostgreSQL")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("events_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    internal async Task<(string DatabaseName, string ConnectionString)> CreateDatabaseAsync()
    {
        var databaseName = $"test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"CREATE DATABASE \"{databaseName}\"",
            connection);
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = databaseName
        };
        return (databaseName, builder.ConnectionString);
    }

    internal async Task DropDatabaseAsync(string databaseName)
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
            connection);
        await command.ExecuteNonQueryAsync();
    }
}

internal sealed class PostgreSqlTestDatabase<TContext>(
    PostgreSqlFixture fixture,
    string databaseName,
    string connectionString,
    TContext context) : IAsyncDisposable
    where TContext : DbContext
{
    public string ConnectionString { get; } = connectionString;
    public TContext Context { get; } = context;

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await fixture.DropDatabaseAsync(databaseName);
    }
}

internal static class PostgreSqlTestDatabase
{
    public static async Task<PostgreSqlTestDatabase<UsersDbContext>> CreateUsersAsync(
        PostgreSqlFixture fixture)
    {
        var (databaseName, connectionString) = await fixture.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var context = new UsersDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new PostgreSqlTestDatabase<UsersDbContext>(
            fixture,
            databaseName,
            connectionString,
            context);
    }

    public static async Task<PostgreSqlTestDatabase<BookingsDbContext>> CreateBookingsAsync(
        PostgreSqlFixture fixture)
    {
        var (databaseName, connectionString) = await fixture.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var context = new BookingsDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new PostgreSqlTestDatabase<BookingsDbContext>(
            fixture,
            databaseName,
            connectionString,
            context);
    }

    public static async Task<PostgreSqlTestDatabase<AppDbContext>> CreateEventsAsync(
        PostgreSqlFixture fixture)
    {
        var (databaseName, connectionString) = await fixture.CreateDatabaseAsync();
        var context = CreateEventsContext(connectionString);
        await context.Database.EnsureCreatedAsync();
        return new PostgreSqlTestDatabase<AppDbContext>(
            fixture,
            databaseName,
            connectionString,
            context);
    }

    public static AppDbContext CreateEventsContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options);
    }
}
