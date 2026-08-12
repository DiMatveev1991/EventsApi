using EventsApi.Application.Dtos;

namespace EventsApi.Tests;

internal static class TestData
{
    public static CreateEventDto CreateEvent(
        string title = "Test Event",
        string? description = "Test description",
        DateTimeOffset? startAt = null,
        DateTimeOffset? endAt = null,
        int? totalSeats = 100) =>
        new()
        {
            Title = title,
            Description = description,
            StartAt = startAt ?? new DateTimeOffset(2030, 6, 1, 10, 0, 0, TimeSpan.Zero),
            EndAt = endAt ?? new DateTimeOffset(2030, 6, 1, 18, 0, 0, TimeSpan.Zero),
            TotalSeats = totalSeats
        };

    public static UpdateEventDto UpdateEvent(
        string title = "Updated Event",
        string? description = "Updated description",
        DateTimeOffset? startAt = null,
        DateTimeOffset? endAt = null) =>
        new()
        {
            Title = title,
            Description = description,
            StartAt = startAt ?? new DateTimeOffset(2030, 7, 1, 10, 0, 0, TimeSpan.Zero),
            EndAt = endAt ?? new DateTimeOffset(2030, 7, 1, 18, 0, 0, TimeSpan.Zero)
        };

    public static EventQueryParameters Query(
        string? title = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 10) =>
        new()
        {
            Title = title,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize
        };
}
