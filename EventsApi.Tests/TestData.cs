using EventsApi.DTOs;

namespace EventsApi.Tests;

internal static class TestData
{
    public static CreateEventDto CreateDto(
        string title = "Test Event",
        string? description = "Test description",
        DateTime? startAt = null,
        DateTime? endAt = null) =>
        new()
        {
            Title = title,
            Description = description,
            StartAt = startAt ?? new DateTime(2025, 06, 01, 10, 00, 00),
            EndAt = endAt ?? new DateTime(2025, 06, 01, 18, 00, 00)
        };

    public static UpdateEventDto UpdateDto(
        string title = "Updated Event",
        string? description = "Updated description",
        DateTime? startAt = null,
        DateTime? endAt = null) =>
        new()
        {
            Title = title,
            Description = description,
            StartAt = startAt ?? new DateTime(2025, 07, 01, 10, 00, 00),
            EndAt = endAt ?? new DateTime(2025, 07, 01, 18, 00, 00)
        };

    public static EventQueryParameters Query(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
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
