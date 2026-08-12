using Contracts;
using EventsApi.Application.Messaging;
using EventsApi.Domain.Entities;
using EventsApi.Infrastructure.Persistence;
using EventsApi.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventsApi.Tests;

public sealed class IdempotencyTests
{
    [Fact]
    public async Task Duplicate_booking_message_decrements_seats_once()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var ev = Event.Create(
            "Kafka test",
            null,
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(2),
            5);
        context.Events.Add(ev);
        await context.SaveChangesAsync();

        var repository = new EventRepository(context);
        var message = new BookingConfirmed(
            Guid.NewGuid(),
            ev.Id,
            Guid.NewGuid(),
            2,
            DateTimeOffset.UtcNow);

        var first = await repository.ApplyBookingConfirmedAsync(message);
        var duplicate = await repository.ApplyBookingConfirmedAsync(message);

        first.Should().Be(BookingConfirmationResult.Applied);
        duplicate.Should().Be(BookingConfirmationResult.Duplicate);
        ev.AvailableSeats.Should().Be(3);
        (await context.ProcessedBookingMessages.CountAsync()).Should().Be(1);
    }
}
