using Bookings.Infrastructure.Persistence;
using EventsApi.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Users.Infrastructure.Persistence;

namespace EventsApi.IntegrationTests.Infrastructure;

internal sealed class SqliteTestDatabase<TContext>(
    SqliteConnection connection,
    TContext context) : IAsyncDisposable
    where TContext : DbContext
{
    public SqliteConnection Connection { get; } = connection;
    public TContext Context { get; } = context;

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await Connection.DisposeAsync();
    }
}

internal static class SqliteTestDatabase
{
    public static async Task<SqliteTestDatabase<UsersDbContext>> CreateUsersAsync()
    {
        var connection = await OpenAsync();
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new UsersDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new SqliteTestDatabase<UsersDbContext>(connection, context);
    }

    public static async Task<SqliteTestDatabase<BookingsDbContext>> CreateBookingsAsync()
    {
        var connection = await OpenAsync();
        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new BookingsDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new SqliteTestDatabase<BookingsDbContext>(connection, context);
    }

    public static async Task<SqliteTestDatabase<AppDbContext>> CreateEventsAsync()
    {
        var connection = await OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new SqliteTestDatabase<AppDbContext>(connection, context);
    }

    private static async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }
}
