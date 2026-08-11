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
    public async Task Create_persists_user_and_event_identifiers()
    {
        var repository = new FakeBookingRepository();
        var service = new BookingService(repository);
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var result = await service.CreateAsync(
            new CreateBookingRequest { EventId = eventId, Seats = 2 },
            userId);

        result.EventId.Should().Be(eventId);
        result.UserId.Should().Be(userId);
        result.Status.Should().Be(BookingStatus.Pending);
        repository.Stored.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_enforces_active_booking_limit()
    {
        var repository = new FakeBookingRepository
        {
            ActiveCount = BookingService.MaxActiveBookingsPerUser
        };
        var service = new BookingService(repository);

        var action = () => service.CreateAsync(
            new CreateBookingRequest { EventId = Guid.NewGuid() },
            Guid.NewGuid());

        await action.Should().ThrowAsync<ActiveBookingLimitExceededException>();
    }

    [Fact]
    public async Task Get_rejects_another_users_booking()
    {
        var repository = new FakeBookingRepository
        {
            Stored = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1)
        };
        var service = new BookingService(repository);

        var action = () => service.GetByIdAsync(
            repository.Stored.Id,
            Guid.NewGuid(),
            false);

        await action.Should().ThrowAsync<ForbiddenException>();
    }

    private sealed class FakeBookingRepository : IBookingRepository
    {
        public Booking? Stored { get; set; }
        public int ActiveCount { get; init; }

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
            Task.FromResult(ActiveCount);

        public Task AddAsync(
            Booking booking,
            CancellationToken cancellationToken = default)
        {
            Stored = booking;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
