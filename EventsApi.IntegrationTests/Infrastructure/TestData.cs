using EventsApi.Models;

namespace EventsApi.IntegrationTests.Infrastructure
{
	/// <summary>Фабрики тестовых сущностей для интеграционных тестов.</summary>
	internal static class TestData
	{
		/// <summary>
		/// Создаёт событие. Даты — с <c>Kind=Unspecified</c>, что соответствует
		/// колонкам типа <c>timestamp without time zone</c>.
		/// </summary>
		public static Event Event(
			string title = "Event",
			DateTime? startAt = null,
			DateTime? endAt = null,
			int totalSeats = 10,
			string? description = null)
		{
			var start = startAt ?? new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);
			var end = endAt ?? start.AddHours(2);
			return Models.Event.Create(title, description, start, end, totalSeats);
		}
	}
}
