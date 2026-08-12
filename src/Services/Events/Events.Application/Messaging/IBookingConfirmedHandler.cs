using Contracts;

namespace EventsApi.Application.Messaging;

public interface IBookingConfirmedHandler
{
    Task<BookingConfirmationResult> HandleAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default);
}
