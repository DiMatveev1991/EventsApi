using Bookings.Application.Abstractions;
using Bookings.Application.Dtos;
using Bookings.Application.Services;
using Bookings.Domain.Entities;
using Bookings.Domain.Enums;
using Bookings.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

public sealed class BookingServiceTests
{
    [Fact]
    public async Task Create_persists_user_event_and_seat_count()
    {
        var repository = new FakeBookingRepository();
        var service = new BookingService(repository);
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var result = await service.CreateAsync(
            new CreateBookingRequest { EventId = eventId, Seats = 2 }, userId);

        result.EventId.Should().Be(eventId);
        result.UserId.Should().Be(userId);
        result.Seats.Should().Be(2);
        result.Status.Should().Be(BookingStatus.Pending);
        repository.Stored.Should().NotBeNull();
        repository.AddCalls.Should().Be(1);
    }

    [Fact]
    public async Task Create_generates_unique_booking_ids()
    {
        var repository = new FakeBookingRepository();
        var service = new BookingService(repository);

        var first = await service.CreateAsync(
            new CreateBookingRequest { EventId = Guid.NewGuid() }, Guid.NewGuid());
        var second = await service.CreateAsync(
            new CreateBookingRequest { EventId = Guid.NewGuid() }, Guid.NewGuid());

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public async Task Create_allows_one_below_active_limit()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeBookingRepository();
        repository.ActiveCounts[userId] = BookingService.MaxActiveBookingsPerUser - 1;
        var service = new BookingService(repository);

        var result = await service.CreateAsync(
            new CreateBookingRequest { EventId = Guid.NewGuid() }, userId);

        result.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task Create_rejects_active_booking_limit_without_persisting()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeBookingRepository();
        repository.ActiveCounts[userId] = BookingService.MaxActiveBookingsPerUser;
        var service = new BookingService(repository);

        var action = () => service.CreateAsync(
            new CreateBookingRequest { EventId = Guid.NewGuid() }, userId);

        await action.Should().ThrowAsync<ActiveBookingLimitExceededException>();
        repository.AddCalls.Should().Be(0);
    }

    [Fact]
    public async Task Active_limit_for_one_user_does_not_affect_another_user()
    {
        var limitedUser = Guid.NewGuid();
        var anotherUser = Guid.NewGuid();
        var repository = new FakeBookingRepository();
        repository.ActiveCounts[limitedUser] = BookingService.MaxActiveBookingsPerUser;
        var service = new BookingService(repository);

        var result = await service.CreateAsync(
            new CreateBookingRequest { EventId = Guid.NewGuid() }, anotherUser);

        result.UserId.Should().Be(anotherUser);
    }

    [Fact]
    public async Task Get_returns_owners_booking()
    {
        var userId = Guid.NewGuid();
        var booking = Booking.CreatePending(Guid.NewGuid(), userId, 1);
        var service = new BookingService(new FakeBookingRepository { Stored = booking });

        var result = await service.GetByIdAsync(booking.Id, userId, false);

        result.Id.Should().Be(booking.Id);
    }

    [Fact]
    public async Task Get_allows_admin_to_read_another_users_booking()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        var service = new BookingService(new FakeBookingRepository { Stored = booking });

        var result = await service.GetByIdAsync(booking.Id, Guid.NewGuid(), true);

        result.Id.Should().Be(booking.Id);
    }

    [Fact]
    public async Task Get_rejects_another_users_booking()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        var service = new BookingService(new FakeBookingRepository { Stored = booking });

        var action = () => service.GetByIdAsync(booking.Id, Guid.NewGuid(), false);

        await action.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Get_unknown_booking_throws_not_found()
    {
        var service = new BookingService(new FakeBookingRepository());

        var action = () => service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), false);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Cancel_owner_changes_status_and_saves()
    {
        var userId = Guid.NewGuid();
        var booking = Booking.CreatePending(Guid.NewGuid(), userId, 1);
        var repository = new FakeBookingRepository { Stored = booking };
        var service = new BookingService(repository);

        await service.CancelAsync(booking.Id, userId, false);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        repository.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Cancel_allows_admin_for_another_users_booking()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        var repository = new FakeBookingRepository { Stored = booking };
        var service = new BookingService(repository);

        await service.CancelAsync(booking.Id, Guid.NewGuid(), true);

        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_rejects_another_user_without_saving()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        var repository = new FakeBookingRepository { Stored = booking };
        var service = new BookingService(repository);

        var action = () => service.CancelAsync(booking.Id, Guid.NewGuid(), false);

        await action.Should().ThrowAsync<ForbiddenException>();
        repository.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Cancel_unknown_booking_throws_not_found()
    {
        var service = new BookingService(new FakeBookingRepository());

        var action = () => service.CancelAsync(Guid.NewGuid(), Guid.NewGuid(), false);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Cancel_repeated_booking_throws_validation_error()
    {
        var userId = Guid.NewGuid();
        var booking = Booking.CreatePending(Guid.NewGuid(), userId, 1);
        booking.Cancel(DateTimeOffset.UtcNow);
        var service = new BookingService(new FakeBookingRepository { Stored = booking });

        var action = () => service.CancelAsync(booking.Id, userId, false);

        await action.Should().ThrowAsync<ValidationException>();
    }

    private sealed class FakeBookingRepository : IBookingRepository
    {
        public Booking? Stored { get; set; }
        public Dictionary<Guid, int> ActiveCounts { get; } = new();
        public int AddCalls { get; private set; }
        public int SaveCalls { get; private set; }

        public Task<Booking?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored?.Id == id ? Stored : null);

        public Task<IReadOnlyList<Guid>> GetPendingIdsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());

        public Task<IReadOnlyList<Guid>> GetUnpublishedConfirmedIdsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());

        public Task<int> CountActiveByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ActiveCounts.GetValueOrDefault(userId));

        public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            Stored = booking;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }
}
