using EventsApi.Infrastructure.Persistence;
using Xunit;

namespace EventsApi.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Базовый класс интеграционных тестов: подключается к общему контейнеру PostgreSQL
    /// и перед каждым тестом (<see cref="IAsyncLifetime.InitializeAsync"/>) приводит базу
    /// к чистому состоянию, обеспечивая изоляцию.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected PostgresDatabaseFixture Fixture { get; }

        protected IntegrationTestBase(PostgresDatabaseFixture fixture)
        {
            Fixture = fixture;
        }

        public async Task InitializeAsync() => await Fixture.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>Новый контекст к контейнерной базе (для Arrange/Assert в отдельных контекстах).</summary>
        protected AppDbContext CreateContext() => Fixture.CreateContext();
    }
}
