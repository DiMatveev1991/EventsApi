using EventsApi.Application.Services;
using EventsApi.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.Tests;

public sealed class EventServiceValidationTests : IDisposable
{
    private readonly ServiceProvider _provider = EventTestHost.Build();
    private IEventService Service => _provider.GetRequiredService<IEventService>();

    public void Dispose() => _provider.Dispose();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_rejects_empty_title(string? title)
    {
        var action = () => Service.CreateAsync(TestData.CreateEvent(title: title!));

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("Title");
    }

    [Fact]
    public async Task Create_rejects_end_before_start()
    {
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var action = () => Service.CreateAsync(
            TestData.CreateEvent(startAt: start, endAt: start.AddMinutes(-1)));

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("EndAt");
    }

    [Fact]
    public async Task Create_rejects_equal_start_and_end()
    {
        var at = DateTimeOffset.UtcNow.AddDays(2);
        var action = () => Service.CreateAsync(TestData.CreateEvent(startAt: at, endAt: at));

        await action.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Create_rejects_non_positive_capacity(int seats)
    {
        var action = () => Service.CreateAsync(TestData.CreateEvent(totalSeats: seats));

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("TotalSeats");
    }

    [Fact]
    public async Task Create_rejects_missing_capacity()
    {
        var action = () => Service.CreateAsync(TestData.CreateEvent(totalSeats: null));

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("TotalSeats");
    }

    [Fact]
    public async Task Update_rejects_end_before_start()
    {
        var existing = await Service.CreateAsync(TestData.CreateEvent());
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var action = () => Service.UpdateAsync(
            existing.Id,
            TestData.UpdateEvent(startAt: start, endAt: start.AddMinutes(-1)));

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("EndAt");
    }

    [Fact]
    public async Task Update_rejects_empty_title()
    {
        var existing = await Service.CreateAsync(TestData.CreateEvent());
        var action = () => Service.UpdateAsync(existing.Id, TestData.UpdateEvent(title: ""));

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("Title");
    }

    [Fact]
    public async Task Create_rejects_null_dto()
    {
        var action = () => Service.CreateAsync(null!);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Update_rejects_null_dto()
    {
        var existing = await Service.CreateAsync(TestData.CreateEvent());
        var action = () => Service.UpdateAsync(existing.Id, null!);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetAll_rejects_null_query()
    {
        var action = () => Service.GetAllAsync(null!);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }
}
