using EventsApi.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.Tests;

public class EventServiceFilteringTests : IAsyncLifetime
{
    private readonly ServiceProvider _sp;
    private readonly IEventService _sut;

    public EventServiceFilteringTests()
    {
        _sp = TestHost.Build();
        _sut = _sp.GetRequiredService<IEventService>();
    }

    public async Task InitializeAsync()
    {
        // Набор данных для всех тестов фильтрации
        await _sut.CreateAsync(TestData.CreateDto(
            title: "DevDays Конференция",
            startAt: new DateTime(2025, 06, 01, 10, 00, 00),
            endAt: new DateTime(2025, 06, 01, 18, 00, 00)));

        await _sut.CreateAsync(TestData.CreateDto(
            title: "C# Митап",
            startAt: new DateTime(2025, 07, 15, 18, 00, 00),
            endAt: new DateTime(2025, 07, 15, 20, 00, 00)));

        await _sut.CreateAsync(TestData.CreateDto(
            title: "JS Митап",
            startAt: new DateTime(2025, 08, 20, 18, 00, 00),
            endAt: new DateTime(2025, 08, 20, 20, 00, 00)));

        await _sut.CreateAsync(TestData.CreateDto(
            title: "Новогодний вечер",
            startAt: new DateTime(2025, 12, 31, 20, 00, 00),
            endAt: new DateTime(2025, 12, 31, 23, 59, 00)));
    }

    public Task DisposeAsync()
    {
        _sp.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Filter_ByTitle_PartialMatch_IsCaseInsensitive()
    {
        // Act
        var result = await _sut.GetAllAsync(TestData.Query(title: "митап"));

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(e => e.Title.Contains("Митап"));
    }

    [Fact]
    public async Task Filter_ByTitle_NoMatches_ReturnsEmpty()
    {
        var result = await _sut.GetAllAsync(TestData.Query(title: "балет"));

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Filter_ByTitle_EmptyOrWhitespace_Ignored()
    {
        // Пустая и пробельная строка — как будто фильтр не передан
        var empty = await _sut.GetAllAsync(TestData.Query(title: ""));
        var whitespace = await _sut.GetAllAsync(TestData.Query(title: "   "));

        empty.TotalCount.Should().Be(4);
        whitespace.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task Filter_From_ReturnsOnlyEventsStartingAtOrAfter()
    {
        // Act — начиная с 15 июля 2025
        var result = await _sut.GetAllAsync(TestData.Query(from: new DateTime(2025, 07, 15)));

        // Assert — должны попасть C# Митап, JS Митап, Новогодний вечер
        result.TotalCount.Should().Be(3);
        result.Items.Should().OnlyContain(e => e.StartAt >= new DateTime(2025, 07, 15));
    }

    [Fact]
    public async Task Filter_To_ReturnsOnlyEventsEndingAtOrBefore()
    {
        // Act — не позже конца августа
        var result = await _sut.GetAllAsync(TestData.Query(to: new DateTime(2025, 08, 31, 23, 59, 59)));

        // Assert — Новогодний вечер отсечён
        result.TotalCount.Should().Be(3);
        result.Items.Should().OnlyContain(e => e.EndAt <= new DateTime(2025, 08, 31, 23, 59, 59));
    }

    [Fact]
    public async Task Filter_FromAndTo_CombinedAsLogicalAnd()
    {
        // Только события, которые и начинаются не раньше 1 июля, и заканчиваются не позже конца августа
        var result = await _sut.GetAllAsync(TestData.Query(
            from: new DateTime(2025, 07, 01),
            to: new DateTime(2025, 08, 31, 23, 59, 59)));

        // Подходят: C# Митап (15 июля) и JS Митап (20 августа)
        result.TotalCount.Should().Be(2);
        result.Items.Select(e => e.Title).Should().BeEquivalentTo(new[] { "C# Митап", "JS Митап" });
    }

    [Fact]
    public async Task Filter_ByTitleAndDates_CombinedAsLogicalAnd()
    {
        // title=митап + диапазон июль–август → только C# Митап и JS Митап
        var result = await _sut.GetAllAsync(TestData.Query(
            title: "Митап",
            from: new DateTime(2025, 07, 01),
            to: new DateTime(2025, 08, 31, 23, 59, 59)));

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(e =>
            e.Title.Contains("Митап", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Filter_FromBoundary_IsInclusive()
    {
        // Точное совпадение с датой начала события
        var result = await _sut.GetAllAsync(TestData.Query(from: new DateTime(2025, 07, 15, 18, 00, 00)));

        // C# Митап стартует ровно в это время — должен быть включён
        result.Items.Should().Contain(e => e.Title == "C# Митап");
    }

    [Fact]
    public async Task Filter_ToBoundary_IsInclusive()
    {
        // Точное совпадение с датой окончания события
        var result = await _sut.GetAllAsync(TestData.Query(to: new DateTime(2025, 06, 01, 18, 00, 00)));

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(e => e.Title == "DevDays Конференция");
    }
}
