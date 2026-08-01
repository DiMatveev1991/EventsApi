using EventsApi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace EventsApi.IntegrationTests
{
	/// <summary>
	/// Тесты, проверяющие, что схема формируется миграциями EF Core: создаются
	/// таблицы events и bookings, между ними настроен внешний ключ, а сама миграция
	/// зафиксирована в истории.
	/// </summary>
	public sealed class MigrationTests : IntegrationTestBase
	{
		public MigrationTests(PostgresDatabaseFixture fixture) : base(fixture) { }

		[Fact]
		public async Task Migration_creates_events_and_bookings_tables()
		{
			// Arrange
			await using var ctx = CreateContext();

			// Act
			var tables = await QueryStringsAsync(ctx,
				"SELECT table_name FROM information_schema.tables " +
				"WHERE table_schema = 'public' AND table_type = 'BASE TABLE';");

			// Assert
			tables.Should().Contain("events");
			tables.Should().Contain("bookings");
		}

		[Fact]
		public async Task Migration_is_recorded_in_history()
		{
			// Arrange
			await using var ctx = CreateContext();

			// Act — миграции, применённые к базе, должны совпадать с миграциями сборки
			var applied = await ctx.Database.GetAppliedMigrationsAsync();

			// Assert
			applied.Should().ContainSingle(m => m.EndsWith("InitialCreate"));
			(await ctx.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
		}

		[Fact]
		public async Task Migration_creates_foreign_key_from_bookings_to_events()
		{
			// Arrange
			await using var ctx = CreateContext();

			// Act — читаем реальные ограничения внешнего ключа из системного каталога
			var fk = await QuerySingleAsync(ctx,
				"SELECT tc.constraint_name || ':' || ccu.table_name || '.' || ccu.column_name " +
				"FROM information_schema.table_constraints tc " +
				"JOIN information_schema.constraint_column_usage ccu " +
				"  ON tc.constraint_name = ccu.constraint_name " +
				"WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_name = 'bookings';");

			// Assert — внешний ключ ссылается на events.Id
			fk.Should().NotBeNull();
			fk!.Should().Contain("events.Id");
		}

		[Fact]
		public async Task Migration_creates_index_on_booking_event_id()
		{
			// Arrange
			await using var ctx = CreateContext();

			// Act
			var indexes = await QueryStringsAsync(ctx,
				"SELECT indexname FROM pg_indexes WHERE tablename = 'bookings';");

			// Assert
			indexes.Should().Contain("IX_bookings_EventId");
		}

		private static async Task<List<string>> QueryStringsAsync(DbContext ctx, string sql)
		{
			var results = new List<string>();
			var connection = (NpgsqlConnection)ctx.Database.GetDbConnection();
			await connection.OpenAsync();
			try
			{
				await using var command = connection.CreateCommand();
				command.CommandText = sql;
				await using var reader = await command.ExecuteReaderAsync();
				while (await reader.ReadAsync())
					results.Add(reader.GetString(0));
			}
			finally
			{
				await connection.CloseAsync();
			}

			return results;
		}

		private static async Task<string?> QuerySingleAsync(DbContext ctx, string sql)
		{
			var rows = await QueryStringsAsync(ctx, sql);
			return rows.FirstOrDefault();
		}
	}
}
