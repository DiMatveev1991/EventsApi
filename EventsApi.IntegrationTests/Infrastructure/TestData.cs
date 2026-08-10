using EventsApi.Domain.Entities;

namespace EventsApi.IntegrationTests.Infrastructure
{
    /// <summary>Фабрики тестовых сущностей для интеграционных тестов.</summary>
    internal static class TestData
    {
        /// <summary>
        /// Создаёт событие с UTC-датами для колонок timestamptz.
        /// </summary>
        public static Event Event(
            string title = "Event",
            DateTimeOffset? startAt = null,
            DateTimeOffset? endAt = null,
            int totalSeats = 10,
            string? description = null)
        {
            var start = startAt ?? new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero);
            var end = endAt ?? start.AddHours(2);
            return EventsApi.Domain.Entities.Event.Create(title, description, start, end, totalSeats);
        }
    }
}
