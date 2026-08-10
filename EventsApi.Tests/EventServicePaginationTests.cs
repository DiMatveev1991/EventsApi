using EventsApi.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.Tests;

public class EventServicePaginationTests : IAsyncLifetime
{
    private readonly ServiceProvider _sp;
    private readonly IEventService _sut;

    public EventServicePaginationTests()
    {
        _sp = TestHost.Build();
        _sut = _sp.GetRequiredService<IEventService>();
    }

    public async Task InitializeAsync()
    {
        // 25 событий, отсортированных по StartAt
        for (int i = 1; i <= 25; i++)
        {
            await _sut.CreateAsync(TestData.CreateDto(
                title: $"Event {i:D2}",
                startAt: new DateTime(2025, 01, 01).AddDays(i),
                endAt: new DateTime(2025, 01, 01).AddDays(i).AddHours(2)));
        }
    }

    public Task DisposeAsync()
    {
        _sp.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Pagination_DefaultPageAndSize_Returns10Items()
    {
        var result = await _sut.GetAllAsync(TestData.Query());

        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task Pagination_SecondPage_ReturnsNextSlice()
    {
        var result = await _sut.GetAllAsync(TestData.Query(page: 2, pageSize: 10));

        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(2);
        result.Items.Should().HaveCount(10);
        result.Items.First().Title.Should().Be("Event 11");
        result.Items.Last().Title.Should().Be("Event 20");
    }

    [Fact]
    public async Task Pagination_LastPartialPage_ReturnsRemainingItems()
    {
        var result = await _sut.GetAllAsync(TestData.Query(page: 3, pageSize: 10));

        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(3);
        result.Items.Should().HaveCount(5);
        result.Items.First().Title.Should().Be("Event 21");
        result.Items.Last().Title.Should().Be("Event 25");
    }

    [Fact]
    public async Task Pagination_PageBeyondTotal_ReturnsEmptyButCorrectTotal()
    {
        var result = await _sut.GetAllAsync(TestData.Query(page: 99, pageSize: 10));

        result.TotalCount.Should().Be(25);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Pagination_CustomPageSize_IsRespected()
    {
        var result = await _sut.GetAllAsync(TestData.Query(page: 1, pageSize: 5));

        result.PageSize.Should().Be(5);
        result.Items.Should().HaveCount(5);
        result.Items.First().Title.Should().Be("Event 01");
        result.Items.Last().Title.Should().Be("Event 05");
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-5, 10)]
    public async Task Pagination_InvalidPage_Normalizes_ToFirstPage(int page, int pageSize)
    {
        var result = await _sut.GetAllAsync(TestData.Query(page: page, pageSize: pageSize));

        result.Page.Should().Be(1);
        result.Items.Should().HaveCount(10);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -7)]
    public async Task Pagination_InvalidPageSize_Normalizes_ToDefault(int page, int pageSize)
    {
        var result = await _sut.GetAllAsync(TestData.Query(page: page, pageSize: pageSize));

        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task Pagination_WithFilter_TotalCountReflectsFilteredSet()
    {
        // До фильтра 25, после — 5 (дни 2..6); страница 1 размером 3 → 3 элемента.
        var result = await _sut.GetAllAsync(TestData.Query(
            from: new DateTime(2025, 01, 02),
            to: new DateTime(2025, 01, 06, 23, 59, 59),
            page: 1,
            pageSize: 3));

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(3);
    }
}
