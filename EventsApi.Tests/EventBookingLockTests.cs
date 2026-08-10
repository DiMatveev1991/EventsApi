using EventsApi.Application.Services;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

public sealed class EventBookingLockTests
{
    [Fact]
    public async Task AcquireAsync_BlocksSameEvent_ButAllowsDifferentEvent()
    {
        var sut = new EventBookingLock();
        var eventId = Guid.NewGuid();
        var first = await sut.AcquireAsync(eventId);

        var sameEvent = sut.AcquireAsync(eventId).AsTask();
        var otherEvent = sut.AcquireAsync(Guid.NewGuid()).AsTask();

        using var otherLease = await otherEvent.WaitAsync(TimeSpan.FromSeconds(1));
        sameEvent.IsCompleted.Should().BeFalse();

        first.Dispose();
        using var sameLease = await sameEvent.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
