using EventsApi.Services;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

public class EventServicePaginationTests
{
    private readonly EventService _sut;

    public EventServicePaginationTests()
    {
        _sut = new EventService();
        // 25 событий, отсортированных по StartAt
        for (int i = 1; i <= 25; i++)
        {
            _sut.Create(TestData.CreateDto(
                title: $"Event {i:D2}",
                startAt: new DateTime(2025, 01, 01).AddDays(i),
                endAt:   new DateTime(2025, 01, 01).AddDays(i).AddHours(2)));
        }
    }

    [Fact]
    public void Pagination_DefaultPageAndSize_Returns10Items()
    {
        var result = _sut.GetAll(TestData.Query());

        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(10);
    }

    [Fact]
    public void Pagination_SecondPage_ReturnsNextSlice()
    {
        var result = _sut.GetAll(TestData.Query(page: 2, pageSize: 10));

        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(2);
        result.Items.Should().HaveCount(10);
        result.Items.First().Title.Should().Be("Event 11");
        result.Items.Last().Title.Should().Be("Event 20");
    }

    [Fact]
    public void Pagination_LastPartialPage_ReturnsRemainingItems()
    {
        var result = _sut.GetAll(TestData.Query(page: 3, pageSize: 10));

        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(3);
        result.Items.Should().HaveCount(5);
        result.Items.First().Title.Should().Be("Event 21");
        result.Items.Last().Title.Should().Be("Event 25");
    }

    [Fact]
    public void Pagination_PageBeyondTotal_ReturnsEmptyButCorrectTotal()
    {
        var result = _sut.GetAll(TestData.Query(page: 99, pageSize: 10));

        result.TotalCount.Should().Be(25);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Pagination_CustomPageSize_IsRespected()
    {
        var result = _sut.GetAll(TestData.Query(page: 1, pageSize: 5));

        result.PageSize.Should().Be(5);
        result.Items.Should().HaveCount(5);
        result.Items.First().Title.Should().Be("Event 01");
        result.Items.Last().Title.Should().Be("Event 05");
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-5, 10)]
    public void Pagination_InvalidPage_Normalizes_ToFirstPage(int page, int pageSize)
    {
        var result = _sut.GetAll(TestData.Query(page: page, pageSize: pageSize));

        result.Page.Should().Be(1);
        result.Items.Should().HaveCount(10);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -7)]
    public void Pagination_InvalidPageSize_Normalizes_ToDefault(int page, int pageSize)
    {
        var result = _sut.GetAll(TestData.Query(page: page, pageSize: pageSize));

        result.PageSize.Should().Be(10);
        result.Items.Should().HaveCount(10);
    }

    [Fact]
    public void Pagination_WithFilter_TotalCountReflectsFilteredSet()
    {
        // Отфильтруем только половину (Event 01..Event 09 + Event 10..Event 25 → 25 штук содержат "Event")
        // Возьмём фильтр по диапазону, оставляющий только первые 5
        var result = _sut.GetAll(TestData.Query(
            from: new DateTime(2025, 01, 02),
            to:   new DateTime(2025, 01, 06, 23, 59, 59),
            page: 1,
            pageSize: 3));

        // До фильтра 25, после — 5 (дни 2..6); страница 1 размером 3 → 3 элемента.
        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(3);
    }
}
