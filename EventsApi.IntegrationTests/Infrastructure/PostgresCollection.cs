using Xunit;

namespace EventsApi.IntegrationTests.Infrastructure
{
	/// <summary>
	/// Тестовая коллекция, объединяющая все интеграционные тесты вокруг одного
	/// контейнера PostgreSQL. Классы одной коллекции xUnit выполняет последовательно,
	/// поэтому пересоздание базы между тестами безопасно.
	/// </summary>
	[CollectionDefinition(Name)]
	public sealed class PostgresCollection : ICollectionFixture<PostgresDatabaseFixture>
	{
		public const string Name = "postgres-integration";
	}
}
