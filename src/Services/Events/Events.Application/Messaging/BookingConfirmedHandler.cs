using Contracts;
using EventsApi.Application.Abstractions;

namespace EventsApi.Application.Messaging;

public sealed class BookingConfirmedHandler(IEventRepository events) : IBookingConfirmedHandler
{
    public Task<BookingConfirmationResult> HandleAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default) =>
        events.ApplyBookingConfirmedAsync(message, cancellationToken);
}
