using Bookings.Application.Dtos;

namespace Bookings.Application.Services;

public interface IBookingService
{
    Task<BookingResponse> CreateAsync(
        CreateBookingRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<BookingResponse> GetByIdAsync(
        Guid bookingId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        Guid bookingId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
