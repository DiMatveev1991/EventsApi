using EventsApi.Application.Services;
using EventsApi.Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.Tests;

public class EventServiceValidationTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly IEventService _sut;

    public EventServiceValidationTests()
    {
        _sp = TestHost.Build();
        _sut = _sp.GetRequiredService<IEventService>();
    }

    public void Dispose() => _sp.Dispose();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_WithEmptyOrWhitespaceTitle_ThrowsValidationException(string? title)
    {
        // Arrange
        var dto = TestData.CreateDto(title: title!);

        // Act
        var act = async () => await _sut.CreateAsync(dto);

        // Assert
        (await act.Should()
            .ThrowAsync<ValidationException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status400BadRequest))
            .Which.Errors.Should().ContainKey("Title");
    }

    [Fact]
    public async Task Create_WithEndAtBeforeStartAt_ThrowsValidationException()
    {
        var dto = TestData.CreateDto(
            startAt: new DateTime(2025, 06, 01, 12, 00, 00),
            endAt: new DateTime(2025, 06, 01, 10, 00, 00));

        var act = async () => await _sut.CreateAsync(dto);

        (await act.Should()
            .ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainKey("EndAt");
    }

    [Fact]
    public async Task Create_WithEqualStartAndEnd_ThrowsValidationException()
    {
        var when = new DateTime(2025, 06, 01, 10, 00, 00);
        var dto = TestData.CreateDto(startAt: when, endAt: when);

        var act = async () => await _sut.CreateAsync(dto);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Create_WithNonPositiveTotalSeats_ThrowsValidationException(int totalSeats)
    {
        // Arrange
        var dto = TestData.CreateDto(totalSeats: totalSeats);

        // Act
        var act = async () => await _sut.CreateAsync(dto);

        // Assert
        (await act.Should()
            .ThrowAsync<ValidationException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status400BadRequest))
            .Which.Errors.Should().ContainKey("TotalSeats");
    }

    [Fact]
    public async Task Create_WithNullTotalSeats_ThrowsValidationException()
    {
        // Arrange
        var dto = TestData.CreateDto(totalSeats: null);

        // Act
        var act = async () => await _sut.CreateAsync(dto);

        // Assert
        (await act.Should()
            .ThrowAsync<ValidationException>()
            .Where(ex => ex.StatusCode == StatusCodes.Status400BadRequest))
            .Which.Errors.Should().ContainKey("TotalSeats");
    }

    [Fact]
    public async Task Update_WithEndAtBeforeStartAt_ThrowsValidationException()
    {
        // Arrange
        var existing = await _sut.CreateAsync(TestData.CreateDto());
        var badUpdate = TestData.UpdateDto(
            startAt: new DateTime(2025, 07, 01, 12, 00, 00),
            endAt: new DateTime(2025, 07, 01, 10, 00, 00));

        // Act
        var act = async () => await _sut.UpdateAsync(existing.Id, badUpdate);

        // Assert
        (await act.Should()
            .ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainKey("EndAt");
    }

    [Fact]
    public async Task Update_WithEmptyTitle_ThrowsValidationException()
    {
        var existing = await _sut.CreateAsync(TestData.CreateDto());
        var badUpdate = TestData.UpdateDto(title: "");

        var act = async () => await _sut.UpdateAsync(existing.Id, badUpdate);

        (await act.Should()
            .ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainKey("Title");
    }

    [Fact]
    public async Task Create_WithNullDto_ThrowsArgumentNullException()
    {
        var act = async () => await _sut.CreateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Update_WithNullDto_ThrowsArgumentNullException()
    {
        var existing = await _sut.CreateAsync(TestData.CreateDto());

        var act = async () => await _sut.UpdateAsync(existing.Id, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
