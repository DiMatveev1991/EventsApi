using EventsApi.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.Tests;

public sealed class EventServicePaginationTests : IAsyncLifetime
{
    private readonly ServiceProvider _provider = EventTestHost.Build();
    private IEventService Service => _provider.GetRequiredService<IEventService>();

    public async Task InitializeAsync()
    {
        for (var index = 1; index <= 25; index++)
        {
            var start = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(index);
            await Service.CreateAsync(TestData.CreateEvent(
                $"Event {index:D2}", startAt: start, endAt: start.AddHours(2)));
        }
    }

    public Task DisposeAsync()
    {
        _provider.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Default_page_returns_ten_items()
    {
        var result = await Service.GetAllAsync(TestData.Query());

        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task Second_page_returns_next_slice()
    {
        var result = await Service.GetAllAsync(TestData.Query(page: 2, pageSize: 10));

        result.Items.Should().HaveCount(10);
        result.Items.First().Title.Should().Be("Event 11");
        result.Items.Last().Title.Should().Be("Event 20");
    }

    [Fact]
    public async Task Last_page_can_be_partial()
    {
        var result = await Service.GetAllAsync(TestData.Query(page: 3, pageSize: 10));

        result.Items.Should().HaveCount(5);
        result.Items.First().Title.Should().Be("Event 21");
        result.Items.Last().Title.Should().Be("Event 25");
    }

    [Fact]
    public async Task Page_beyond_total_is_empty_but_keeps_total_count()
    {
        var result = await Service.GetAllAsync(TestData.Query(page: 99));

        result.TotalCount.Should().Be(25);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Custom_page_size_is_respected()
    {
        var result = await Service.GetAllAsync(TestData.Query(pageSize: 5));

        result.PageSize.Should().Be(5);
        result.Items.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Invalid_page_is_normalized_to_one(int page)
    {
        var result = await Service.GetAllAsync(TestData.Query(page: page));

        result.Page.Should().Be(1);
        result.Items.Should().HaveCount(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task Invalid_page_size_is_normalized_to_ten(int pageSize)
    {
        var result = await Service.GetAllAsync(TestData.Query(pageSize: pageSize));

        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task Filtered_pagination_reports_filtered_total()
    {
        var from = new DateTimeOffset(2030, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2030, 1, 6, 23, 59, 59, TimeSpan.Zero);

        var result = await Service.GetAllAsync(TestData.Query(from: from, to: to, pageSize: 3));

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(3);
    }
}
