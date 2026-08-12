using EventsApi.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.Tests;

public sealed class EventServiceFilteringTests : IAsyncLifetime
{
    private readonly ServiceProvider _provider = EventTestHost.Build();
    private IEventService Service => _provider.GetRequiredService<IEventService>();

    public async Task InitializeAsync()
    {
        await Service.CreateAsync(TestData.CreateEvent(
            "DevDays Conference", startAt: At(6, 1, 10), endAt: At(6, 1, 18)));
        await Service.CreateAsync(TestData.CreateEvent(
            "C# Meetup", startAt: At(7, 15, 18), endAt: At(7, 15, 20)));
        await Service.CreateAsync(TestData.CreateEvent(
            "JS Meetup", startAt: At(8, 20, 18), endAt: At(8, 20, 20)));
        await Service.CreateAsync(TestData.CreateEvent(
            "New Year Party", startAt: At(12, 31, 20), endAt: At(12, 31, 23, 59)));
    }

    public Task DisposeAsync()
    {
        _provider.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Title_filter_is_partial_and_case_insensitive()
    {
        var result = await Service.GetAllAsync(TestData.Query(title: "meetUP"));

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(item => item.Title.Contains("Meetup"));
    }

    [Fact]
    public async Task Unknown_title_returns_empty_page()
    {
        var result = await Service.GetAllAsync(TestData.Query(title: "ballet"));

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Empty_title_filter_is_ignored(string? title)
    {
        var result = await Service.GetAllAsync(TestData.Query(title: title));

        result.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task From_filter_returns_events_starting_at_or_after_boundary()
    {
        var boundary = At(7, 15, 18);
        var result = await Service.GetAllAsync(TestData.Query(from: boundary));

        result.TotalCount.Should().Be(3);
        result.Items.Should().OnlyContain(item => item.StartAt >= boundary);
    }

    [Fact]
    public async Task To_filter_returns_events_ending_at_or_before_boundary()
    {
        var boundary = At(8, 31, 23, 59);
        var result = await Service.GetAllAsync(TestData.Query(to: boundary));

        result.TotalCount.Should().Be(3);
        result.Items.Should().OnlyContain(item => item.EndAt <= boundary);
    }

    [Fact]
    public async Task Date_filters_are_combined_with_logical_and()
    {
        var result = await Service.GetAllAsync(TestData.Query(
            from: At(7, 1), to: At(8, 31, 23, 59)));

        result.Items.Select(item => item.Title)
            .Should().BeEquivalentTo(new[] { "C# Meetup", "JS Meetup" });
    }

    [Fact]
    public async Task Title_and_date_filters_are_combined()
    {
        var result = await Service.GetAllAsync(TestData.Query(
            title: "meetup", from: At(8, 1), to: At(8, 31, 23, 59)));

        result.Items.Should().ContainSingle(item => item.Title == "JS Meetup");
    }

    [Fact]
    public async Task From_boundary_is_inclusive()
    {
        var result = await Service.GetAllAsync(TestData.Query(from: At(7, 15, 18)));

        result.Items.Should().Contain(item => item.Title == "C# Meetup");
    }

    [Fact]
    public async Task To_boundary_is_inclusive()
    {
        var result = await Service.GetAllAsync(TestData.Query(to: At(6, 1, 18)));

        result.Items.Should().ContainSingle(item => item.Title == "DevDays Conference");
    }

    private static DateTimeOffset At(int month, int day, int hour = 0, int minute = 0) =>
        new(2030, month, day, hour, minute, 0, TimeSpan.Zero);
}
