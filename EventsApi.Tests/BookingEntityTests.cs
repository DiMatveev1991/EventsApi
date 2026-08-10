using EventsApi.Domain.Entities;
using EventsApi.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

public class BookingEntityTests
{
    [Fact]
    public void CreatePending_GeneratesIdAndSetsDefaults()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var booking = Booking.CreatePending(eventId, userId);
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(eventId);
        booking.UserId.Should().Be(userId);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();
        booking.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void CreatePending_TwoCalls_ProduceUniqueIds()
    {
        var a = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        var b = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());

        a.Id.Should().NotBe(b.Id);
    }

    [Fact]
    public void Confirm_FromPending_SetsConfirmedAndProcessedAt()
    {
        // Arrange
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        var processedAt = new DateTime(2025, 06, 01, 10, 00, 00, DateTimeKind.Utc);

        // Act
        booking.Confirm(processedAt);

        // Assert
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().Be(processedAt);
    }

    [Fact]
    public void Reject_FromPending_SetsRejectedAndProcessedAt()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        var processedAt = new DateTime(2025, 06, 01, 10, 00, 00, DateTimeKind.Utc);

        booking.Reject(processedAt);

        booking.Status.Should().Be(BookingStatus.Rejected);
        booking.ProcessedAt.Should().Be(processedAt);
    }

    [Fact]
    public void Confirm_FromNonPending_Throws()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Confirm(DateTime.UtcNow);

        var act = () => booking.Confirm(DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reject_FromNonPending_Throws()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Reject(DateTime.UtcNow);

        var act = () => booking.Reject(DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_FromConfirmed_SetsCancelledAndProcessedAt()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Confirm(DateTime.UtcNow.AddMinutes(-1));
        var cancelledAt = DateTime.UtcNow;

        booking.Cancel(cancelledAt);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.ProcessedAt.Should().Be(cancelledAt);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_Throws()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Cancel(DateTime.UtcNow);

        var act = () => booking.Cancel(DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }
}
