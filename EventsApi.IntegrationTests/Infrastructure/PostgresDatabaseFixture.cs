using EventsApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace EventsApi.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Поднимает один контейнер PostgreSQL на весь набор интеграционных тестов
    /// (через <see cref="ICollectionFixture{TFixture}"/>) и предоставляет фабрику
    /// <see cref="AppDbContext"/> и метод приведения базы к чистому состоянию.
    /// </summary>
    public sealed class PostgresDatabaseFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("eventapi_tests")
            .WithUsername("postgres")
            .WithPassword($"test-{Guid.NewGuid():N}")
            .Build();

        /// <summary>Строка подключения к контейнеру (порт назначается Testcontainers динамически).</summary>
        public string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            // Пул соединений отключаем намеренно: между тестами база пересоздаётся
            // (EnsureDeleted + Migrate), а «висящие» в пуле соединения к уже удалённой
            // базе приводили бы к ошибкам подключения.
            var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
            {
                Pooling = false
            };
            ConnectionString = builder.ConnectionString;
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }

        /// <summary>Создаёт новый экземпляр <see cref="AppDbContext"/> для контейнерной базы.</summary>
        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            return new AppDbContext(options);
        }

        /// <summary>
        /// Приводит базу к чистому состоянию перед каждым тестом: удаляет схему и
        /// пересоздаёт её миграциями EF Core. Это гарантирует независимость тестов
        /// от порядка запуска и отсутствие «протёкших» между тестами данных.
        /// </summary>
        public async Task ResetDatabaseAsync()
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();
        }
    }
}
