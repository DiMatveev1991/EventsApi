using Contracts;
using EventsApi.Application.Messaging;
using EventsApi.Domain.Entities;
using EventsApi.Infrastructure.Repositories;
using EventsApi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventsApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EventRepositoryTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Add_persists_event_and_capacity()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var repository = new EventRepository(database.Context);
        var ev = CreateEvent(5);

        await repository.AddAsync(ev);
        database.Context.ChangeTracker.Clear();
        var stored = await repository.GetByIdAsync(ev.Id);

        stored.Should().NotBeNull();
        stored!.TotalSeats.Should().Be(5);
        stored.AvailableSeats.Should().Be(5);
    }

    [Fact]
    public async Task Get_unknown_event_returns_null()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var repository = new EventRepository(database.Context);

        (await repository.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task Top_popular_orders_by_sold_percentage_and_limits_result()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var repository = new EventRepository(database.Context);
        var events = Enumerable.Range(0, 12)
            .Select(_ => CreateEvent(100))
            .ToList();
        foreach (var (ev, index) in events.Select((item, index) => (item, index)))
            ev.AvailableSeats = ev.TotalSeats - index * 5;
        database.Context.Events.AddRange(events);
        await database.Context.SaveChangesAsync();

        var result = await repository.GetTopPopularAsync(10);

        result.Should().HaveCount(10);
        result.Select(ev =>
                (double)(ev.TotalSeats - ev.AvailableSeats) / ev.TotalSeats)
            .Should().BeInDescendingOrder();
        result.Select(ev => ev.Id).Should().NotContain(events[0].Id);
    }

    [Fact]
    public async Task Update_persists_changed_fields()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var repository = new EventRepository(database.Context);
        var ev = CreateEvent(5);
        await repository.AddAsync(ev);
        ev.Title = "Updated";

        await repository.UpdateAsync(ev);
        database.Context.ChangeTracker.Clear();
        (await repository.GetByIdAsync(ev.Id))!.Title.Should().Be("Updated");
    }

    [Fact]
    public async Task Delete_removes_event()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var repository = new EventRepository(database.Context);
        var ev = CreateEvent(5);
        await repository.AddAsync(ev);

        await repository.DeleteAsync(ev);

        (await repository.GetByIdAsync(ev.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Booking_confirmation_decrements_available_seats()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var repository = new EventRepository(database.Context);
        var ev = CreateEvent(5);
        await repository.AddAsync(ev);

        var result = await repository.ApplyBookingConfirmedAsync(Message(ev.Id, 2));

        result.Should().Be(BookingConfirmationResult.Applied);
        ev.AvailableSeats.Should().Be(3);
    }

    [Fact]
    public async Task Duplicate_booking_confirmation_is_idempotent()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var repository = new EventRepository(database.Context);
        var ev = CreateEvent(5);
        await repository.AddAsync(ev);
        var message = Message(ev.Id, 2);

        var first = await repository.ApplyBookingConfirmedAsync(message);
        var duplicate = await repository.ApplyBookingConfirmedAsync(message);

        first.Should().Be(BookingConfirmationResult.Applied);
        duplicate.Should().Be(BookingConfirmationResult.Duplicate);
        ev.AvailableSeats.Should().Be(3);
        (await database.Context.ProcessedBookingMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_duplicate_confirmation_is_applied_once_on_postgresql()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var setupRepository = new EventRepository(database.Context);
        var ev = CreateEvent(5);
        await setupRepository.AddAsync(ev);
        database.Context.ChangeTracker.Clear();

        await using var secondContext =
            PostgreSqlTestDatabase.CreateEventsContext(database.ConnectionString);
        var message = Message(ev.Id, 2);
        var results = await Task.WhenAll(
            new EventRepository(database.Context).ApplyBookingConfirmedAsync(message),
            new EventRepository(secondContext).ApplyBookingConfirmedAsync(message));

        results.Should().ContainSingle(result => result == BookingConfirmationResult.Applied);
        results.Should().ContainSingle(result => result == BookingConfirmationResult.Duplicate);

        database.Context.ChangeTracker.Clear();
        var stored = await setupRepository.GetByIdAsync(ev.Id);
        stored!.AvailableSeats.Should().Be(3);
        (await database.Context.ProcessedBookingMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Missing_event_is_recorded_and_does_not_break_consumer_flow()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var repository = new EventRepository(database.Context);
        var message = Message(Guid.NewGuid(), 1);

        var result = await repository.ApplyBookingConfirmedAsync(message);

        result.Should().Be(BookingConfirmationResult.EventNotFound);
        (await database.Context.ProcessedBookingMessages.SingleAsync()).Result
            .Should().Be(nameof(BookingConfirmationResult.EventNotFound));
    }

    [Fact]
    public async Task Insufficient_capacity_does_not_make_seats_negative()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var repository = new EventRepository(database.Context);
        var ev = CreateEvent(1);
        await repository.AddAsync(ev);

        var result = await repository.ApplyBookingConfirmedAsync(Message(ev.Id, 2));

        result.Should().Be(BookingConfirmationResult.InsufficientSeats);
        ev.AvailableSeats.Should().Be(1);
    }

    private static Event CreateEvent(int seats) => Event.Create(
        "Conference",
        null,
        DateTimeOffset.UtcNow.AddDays(1),
        DateTimeOffset.UtcNow.AddDays(2),
        seats);

    private static BookingConfirmed Message(Guid eventId, int seats) => new(
        Guid.NewGuid(),
        eventId,
        Guid.NewGuid(),
        seats,
        DateTimeOffset.UtcNow);
}
