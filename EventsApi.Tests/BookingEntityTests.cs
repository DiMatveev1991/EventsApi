using Bookings.Domain.Entities;
using Bookings.Domain.Enums;
using Bookings.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

public sealed class BookingEntityTests
{
    [Fact]
    public void CreatePending_sets_identifiers_and_defaults()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var booking = Booking.CreatePending(eventId, userId, 2);

        booking.Id.Should().NotBeEmpty();
        booking.EventId.Should().Be(eventId);
        booking.UserId.Should().Be(userId);
        booking.Seats.Should().Be(2);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();
        booking.ConfirmationPublishedAt.Should().BeNull();
        booking.CreatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void CreatePending_generates_unique_ids()
    {
        var first = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        var second = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);

        first.Id.Should().NotBe(second.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public void CreatePending_rejects_invalid_seat_count(int seats)
    {
        var action = () => Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), seats);

        action.Should().Throw<ValidationException>();
    }

    [Fact]
    public void CreatePending_accepts_upper_seat_boundary()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 50);

        booking.Seats.Should().Be(50);
    }

    [Theory]
    [MemberData(nameof(EmptyIdentifiers))]
    public void CreatePending_rejects_empty_identifiers(Guid eventId, Guid userId)
    {
        var action = () => Booking.CreatePending(eventId, userId, 1);

        action.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Confirm_sets_status_and_utc_timestamp()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        var at = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.FromHours(3));

        booking.Confirm(at);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().Be(at.ToUniversalTime());
    }

    [Fact]
    public void Confirm_cannot_be_repeated()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        booking.Confirm(DateTimeOffset.UtcNow);

        var action = () => booking.Confirm(DateTimeOffset.UtcNow);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Publish_marker_requires_confirmed_booking()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);

        var action = () => booking.MarkConfirmationPublished(DateTimeOffset.UtcNow);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Publish_marker_records_utc_timestamp()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        booking.Confirm(DateTimeOffset.UtcNow);
        var at = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.FromHours(-5));

        booking.MarkConfirmationPublished(at);

        booking.ConfirmationPublishedAt.Should().Be(at.ToUniversalTime());
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Confirmed)]
    public void Cancel_changes_active_booking_to_cancelled(BookingStatus initialStatus)
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        if (initialStatus == BookingStatus.Confirmed)
            booking.Confirm(DateTimeOffset.UtcNow.AddMinutes(-1));

        booking.Cancel(DateTimeOffset.UtcNow);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_cannot_be_repeated()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        booking.Cancel(DateTimeOffset.UtcNow);

        var action = () => booking.Cancel(DateTimeOffset.UtcNow);

        action.Should().Throw<ValidationException>();
    }

    public static TheoryData<Guid, Guid> EmptyIdentifiers => new()
    {
        { Guid.Empty, Guid.NewGuid() },
        { Guid.NewGuid(), Guid.Empty }
    };
}
