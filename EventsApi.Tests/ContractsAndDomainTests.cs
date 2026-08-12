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
    public void Event_reserves_exact_remaining_capacity()
    {
        var ev = Event.Create(
            "Conference",
            null,
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(2),
            3);

        ev.TryReserveSeats(3).Should().BeTrue();
        ev.AvailableSeats.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Event_rejects_non_positive_reservation_without_mutation(int seats)
    {
        var ev = Event.Create(
            "Conference",
            null,
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(2),
            3);

        ev.TryReserveSeats(seats).Should().BeFalse();
        ev.AvailableSeats.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Event_factory_rejects_non_positive_capacity(int seats)
    {
        var action = () => Event.Create(
            "Conference",
            null,
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(2),
            seats);

        action.Should().Throw<EventsApi.Domain.Exceptions.ValidationException>();
    }

    [Fact]
    public void Event_factory_normalizes_dates_to_utc()
    {
        var start = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.FromHours(3));
        var end = start.AddHours(2);

        var ev = Event.Create("Conference", null, start, end, 3);

        ev.StartAt.Should().Be(start.ToUniversalTime());
        ev.EndAt.Should().Be(end.ToUniversalTime());
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
