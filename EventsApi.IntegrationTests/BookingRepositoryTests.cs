using Bookings.Domain.Entities;
using Bookings.Domain.Enums;
using Bookings.Infrastructure.Repositories;
using EventsApi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace EventsApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class BookingRepositoryTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Add_persists_pending_booking_with_cross_service_ids()
    {
        await using var database = await PostgreSqlTestDatabase.CreateBookingsAsync(fixture);
        var repository = new BookingRepository(database.Context);
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 3);

        await repository.AddAsync(booking);
        database.Context.ChangeTracker.Clear();
        var stored = await repository.GetByIdAsync(booking.Id);

        stored.Should().NotBeNull();
        stored!.EventId.Should().Be(booking.EventId);
        stored.UserId.Should().Be(booking.UserId);
        stored.Seats.Should().Be(3);
        stored.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task Get_unknown_booking_returns_null()
    {
        await using var database = await PostgreSqlTestDatabase.CreateBookingsAsync(fixture);
        var repository = new BookingRepository(database.Context);

        (await repository.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task Save_persists_confirmation_transition()
    {
        await using var database = await PostgreSqlTestDatabase.CreateBookingsAsync(fixture);
        var repository = new BookingRepository(database.Context);
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        await repository.AddAsync(booking);

        booking.Confirm(DateTimeOffset.UtcNow);
        await repository.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        (await repository.GetByIdAsync(booking.Id))!.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Pending_query_returns_only_pending_bookings()
    {
        await using var database = await PostgreSqlTestDatabase.CreateBookingsAsync(fixture);
        var repository = new BookingRepository(database.Context);
        var pending = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        var confirmed = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        confirmed.Confirm(DateTimeOffset.UtcNow);
        var cancelled = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        cancelled.Cancel(DateTimeOffset.UtcNow);
        database.Context.AddRange(pending, confirmed, cancelled);
        await database.Context.SaveChangesAsync();

        var ids = await repository.GetPendingIdsAsync();

        ids.Should().ContainSingle().Which.Should().Be(pending.Id);
    }

    [Fact]
    public async Task Unpublished_query_returns_only_confirmed_unpublished_bookings()
    {
        await using var database = await PostgreSqlTestDatabase.CreateBookingsAsync(fixture);
        var repository = new BookingRepository(database.Context);
        var unpublished = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        unpublished.Confirm(DateTimeOffset.UtcNow);
        var published = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        published.Confirm(DateTimeOffset.UtcNow);
        published.MarkConfirmationPublished(DateTimeOffset.UtcNow);
        var pending = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        database.Context.AddRange(unpublished, published, pending);
        await database.Context.SaveChangesAsync();

        var ids = await repository.GetUnpublishedConfirmedIdsAsync();

        ids.Should().ContainSingle().Which.Should().Be(unpublished.Id);
    }

    [Fact]
    public async Task Active_count_includes_pending_and_confirmed_but_not_cancelled()
    {
        await using var database = await PostgreSqlTestDatabase.CreateBookingsAsync(fixture);
        var repository = new BookingRepository(database.Context);
        var userId = Guid.NewGuid();
        var pending = Booking.CreatePending(Guid.NewGuid(), userId, 1);
        var confirmed = Booking.CreatePending(Guid.NewGuid(), userId, 1);
        confirmed.Confirm(DateTimeOffset.UtcNow);
        var cancelled = Booking.CreatePending(Guid.NewGuid(), userId, 1);
        cancelled.Cancel(DateTimeOffset.UtcNow);
        database.Context.AddRange(pending, confirmed, cancelled);
        await database.Context.SaveChangesAsync();

        var count = await repository.CountActiveByUserIdAsync(userId);

        count.Should().Be(2);
    }

    [Fact]
    public async Task Active_count_is_isolated_per_user()
    {
        await using var database = await PostgreSqlTestDatabase.CreateBookingsAsync(fixture);
        var repository = new BookingRepository(database.Context);
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        database.Context.AddRange(
            Booking.CreatePending(Guid.NewGuid(), firstUser, 1),
            Booking.CreatePending(Guid.NewGuid(), firstUser, 1),
            Booking.CreatePending(Guid.NewGuid(), secondUser, 1));
        await database.Context.SaveChangesAsync();

        (await repository.CountActiveByUserIdAsync(firstUser)).Should().Be(2);
        (await repository.CountActiveByUserIdAsync(secondUser)).Should().Be(1);
    }
}
