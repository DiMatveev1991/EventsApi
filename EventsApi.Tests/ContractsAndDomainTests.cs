using Bookings.Domain.Entities;
using Bookings.Domain.Enums;
using Contracts;
using EventsApi.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

public sealed class ContractsAndDomainTests
{
    [Fact]
    public void BookingConfirmed_contains_only_integration_data()
    {
        var message = new BookingConfirmed(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            DateTimeOffset.UtcNow);

        message.Seats.Should().Be(2);
        KafkaTopics.BookingConfirmed.Should().Be("booking-confirmed");
    }

    [Fact]
    public void Event_reserves_requested_seats()
    {
        var ev = Event.Create(
            "Conference",
            null,
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(2),
            5);

        ev.TryReserveSeats(2).Should().BeTrue();
        ev.AvailableSeats.Should().Be(3);
    }

    [Fact]
    public void Event_does_not_go_below_zero()
    {
        var ev = Event.Create(
            "Conference",
            null,
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(2),
            1);

        ev.TryReserveSeats(2).Should().BeFalse();
        ev.AvailableSeats.Should().Be(1);
    }

    [Fact]
    public void Booking_tracks_confirmation_delivery()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 3);
        var confirmedAt = DateTimeOffset.UtcNow;

        booking.Confirm(confirmedAt);
        booking.MarkConfirmationPublished(confirmedAt.AddSeconds(1));

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().Be(confirmedAt);
        booking.ConfirmationPublishedAt.Should().NotBeNull();
    }

    [Fact]
    public void Booking_rejects_invalid_seat_count()
    {
        var action = () => Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 0);

        action.Should().Throw<Bookings.Domain.Exceptions.ValidationException>();
    }
}
