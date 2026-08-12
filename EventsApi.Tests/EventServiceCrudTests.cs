using EventsApi.Application.Services;
using EventsApi.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.Tests;

public sealed class EventServiceCrudTests : IDisposable
{
    private readonly ServiceProvider _provider = EventTestHost.Build();
    private IEventService Service => _provider.GetRequiredService<IEventService>();

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Create_returns_event_with_generated_id()
    {
        var dto = TestData.CreateEvent(title: "Конференция");

        var created = await Service.CreateAsync(dto);

        created.Id.Should().NotBeEmpty();
        created.Title.Should().Be("Конференция");
        created.Description.Should().Be(dto.Description);
        created.StartAt.Should().Be(dto.StartAt);
        created.EndAt.Should().Be(dto.EndAt);
    }

    [Fact]
    public async Task Create_sets_available_seats_equal_to_total_seats()
    {
        var created = await Service.CreateAsync(TestData.CreateEvent(totalSeats: 7));

        created.TotalSeats.Should().Be(7);
        created.AvailableSeats.Should().Be(7);
    }

    [Fact]
    public async Task Create_trims_title()
    {
        var created = await Service.CreateAsync(TestData.CreateEvent(title: "   Тест   "));

        created.Title.Should().Be("Тест");
    }

    [Fact]
    public async Task GetAll_when_empty_returns_zero_count()
    {
        var result = await Service.GetAllAsync(TestData.Query());

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetAll_without_filters_returns_all_events()
    {
        await Service.CreateAsync(TestData.CreateEvent(title: "Event A"));
        await Service.CreateAsync(TestData.CreateEvent(title: "Event B"));
        await Service.CreateAsync(TestData.CreateEvent(title: "Event C"));

        var result = await Service.GetAllAsync(TestData.Query());

        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetById_returns_existing_event()
    {
        var created = await Service.CreateAsync(TestData.CreateEvent(title: "Нужное"));

        var found = await Service.GetByIdAsync(created.Id);

        found.Id.Should().Be(created.Id);
        found.Title.Should().Be("Нужное");
    }

    [Fact]
    public async Task GetById_for_unknown_id_throws_not_found()
    {
        var id = Guid.NewGuid();

        var action = () => Service.GetByIdAsync(id);

        await action.Should().ThrowAsync<NotFoundException>().WithMessage($"*{id}*");
    }

    [Fact]
    public async Task Update_changes_existing_event()
    {
        var created = await Service.CreateAsync(TestData.CreateEvent(title: "Старое"));
        var dto = TestData.UpdateEvent(title: "Новое", description: "Новое описание");

        var updated = await Service.UpdateAsync(created.Id, dto);

        updated.Title.Should().Be("Новое");
        updated.Description.Should().Be("Новое описание");
        (await Service.GetByIdAsync(created.Id)).Title.Should().Be("Новое");
    }

    [Fact]
    public async Task Update_for_unknown_id_throws_not_found()
    {
        var action = () => Service.UpdateAsync(Guid.NewGuid(), TestData.UpdateEvent());

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_removes_existing_event()
    {
        var created = await Service.CreateAsync(TestData.CreateEvent());

        await Service.DeleteAsync(created.Id);

        var action = () => Service.GetByIdAsync(created.Id);
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_for_unknown_id_throws_not_found()
    {
        var action = () => Service.DeleteAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>();
    }
}
