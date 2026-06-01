using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Models;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

public class InMemoryBookingStoreTests
{
    private readonly InMemoryBookingStore _sut = new();

    [Fact]
    public void Add_NewBooking_CanBeRetrievedById()
    {
        var booking = Booking.CreatePending(Guid.NewGuid());

        _sut.Add(booking);

        _sut.GetById(booking.Id).Should().BeSameAs(booking);
    }

    [Fact]
    public void Add_DuplicateId_Throws()
    {
        var booking = Booking.CreatePending(Guid.NewGuid());
        _sut.Add(booking);

        var duplicate = new Booking
        {
            Id = booking.Id,
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var act = () => _sut.Add(duplicate);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        _sut.GetById(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void GetAll_ReturnsAllBookings()
    {
        var a = Booking.CreatePending(Guid.NewGuid());
        var b = Booking.CreatePending(Guid.NewGuid());
        _sut.Add(a);
        _sut.Add(b);

        _sut.GetAll().Should().HaveCount(2).And.Contain(new[] { a, b });
    }

    [Fact]
    public void GetPending_ReturnsOnlyPendingBookings()
    {
        var pending1 = Booking.CreatePending(Guid.NewGuid());
        var pending2 = Booking.CreatePending(Guid.NewGuid());
        var confirmed = Booking.CreatePending(Guid.NewGuid());
        confirmed.Confirm(DateTime.UtcNow);
        var rejected = Booking.CreatePending(Guid.NewGuid());
        rejected.Reject(DateTime.UtcNow);

        _sut.Add(pending1);
        _sut.Add(pending2);
        _sut.Add(confirmed);
        _sut.Add(rejected);

        var pending = _sut.GetPending();

        pending.Should().HaveCount(2).And.Contain(new[] { pending1, pending2 });
        pending.Should().OnlyContain(b => b.Status == BookingStatus.Pending);
    }

    [Fact]
    public void Update_ExistingBooking_DoesNotThrow()
    {
        var booking = Booking.CreatePending(Guid.NewGuid());
        _sut.Add(booking);

        booking.Confirm(DateTime.UtcNow);
        var act = () => _sut.Update(booking);

        act.Should().NotThrow();
        _sut.GetById(booking.Id)!.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public void Update_UnknownId_Throws()
    {
        var booking = Booking.CreatePending(Guid.NewGuid());

        var act = () => _sut.Update(booking);

        act.Should().Throw<InvalidOperationException>();
    }
}
