using Bookings.Domain.Entities;
using EventsApi.Domain.Entities;
using EventsApi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Users.Domain.Entities;
using Xunit;

namespace EventsApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class DatabaseModelTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Users_has_own_table_and_unique_login_index()
    {
        await using var database = await PostgreSqlTestDatabase.CreateUsersAsync(fixture);
        var entity = database.Context.Model.FindEntityType(typeof(User))!;

        entity.GetTableName().Should().Be("users");
        entity.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(User.Login) }));
        entity.GetForeignKeys().Should().BeEmpty();
    }

    [Fact]
    public async Task Bookings_stores_only_cross_service_identifiers_without_foreign_keys()
    {
        await using var database = await PostgreSqlTestDatabase.CreateBookingsAsync(fixture);
        var entity = database.Context.Model.FindEntityType(typeof(Booking))!;

        entity.GetTableName().Should().Be("bookings");
        entity.FindProperty(nameof(Booking.UserId)).Should().NotBeNull();
        entity.FindProperty(nameof(Booking.EventId)).Should().NotBeNull();
        entity.GetForeignKeys().Should().BeEmpty();
    }

    [Fact]
    public async Task Events_has_inbox_table_for_idempotency()
    {
        await using var database = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);
        var eventEntity = database.Context.Model.FindEntityType(typeof(Event))!;
        var inboxEntity = database.Context.Model.FindEntityType(typeof(ProcessedBookingMessage))!;

        eventEntity.GetTableName().Should().Be("events");
        inboxEntity.GetTableName().Should().Be("processed_booking_messages");
        inboxEntity.FindPrimaryKey()!.Properties.Should()
            .ContainSingle(property => property.Name == nameof(ProcessedBookingMessage.BookingId));
        eventEntity.GetForeignKeys().Should().BeEmpty();
        inboxEntity.GetForeignKeys().Should().BeEmpty();
    }

    [Fact]
    public async Task Every_service_exposes_its_own_initial_migration()
    {
        await using var users = await PostgreSqlTestDatabase.CreateUsersAsync(fixture);
        await using var bookings = await PostgreSqlTestDatabase.CreateBookingsAsync(fixture);
        await using var events = await PostgreSqlTestDatabase.CreateEventsAsync(fixture);

        users.Context.Database.GetMigrations().Should().ContainSingle()
            .Which.Should().Be("20260811000100_InitialUsers");
        bookings.Context.Database.GetMigrations().Should().ContainSingle()
            .Which.Should().Be("20260811000200_InitialBookings");
        events.Context.Database.GetMigrations().Should().ContainSingle()
            .Which.Should().Be("20260811000300_InitialEvents");
    }
}
