using EventsApi.Application.Dtos;

namespace EventsApi.Tests;

internal static class TestData
{
    public static CreateEventDto CreateDto(
        string title = "Test Event",
        string? description = "Test description",
        DateTimeOffset? startAt = null,
        DateTimeOffset? endAt = null,
        int? totalSeats = 100) =>
        new()
        {
            Title = title,
            Description = description,
            StartAt = startAt ?? new DateTimeOffset(2025, 06, 01, 10, 00, 00, TimeSpan.Zero),
            EndAt = endAt ?? new DateTimeOffset(2025, 06, 01, 18, 00, 00, TimeSpan.Zero),
            TotalSeats = totalSeats
        };

    public static UpdateEventDto UpdateDto(
        string title = "Updated Event",
        string? description = "Updated description",
        DateTimeOffset? startAt = null,
        DateTimeOffset? endAt = null) =>
        new()
        {
            Title = title,
            Description = description,
            StartAt = startAt ?? new DateTimeOffset(2025, 07, 01, 10, 00, 00, TimeSpan.Zero),
            EndAt = endAt ?? new DateTimeOffset(2025, 07, 01, 18, 00, 00, TimeSpan.Zero)
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
