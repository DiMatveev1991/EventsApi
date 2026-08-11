using Bookings.Application.Abstractions;
using Bookings.Application.Dtos;
using Bookings.Domain.Entities;
using Bookings.Domain.Exceptions;

namespace Bookings.Application.Services;

public sealed class BookingService(IBookingRepository bookings) : IBookingService
{
    public const int MaxActiveBookingsPerUser = 10;

    public async Task<BookingResponse> CreateAsync(
        CreateBookingRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var activeCount = await bookings.CountActiveByUserIdAsync(userId, cancellationToken);
        if (activeCount >= MaxActiveBookingsPerUser)
            throw new ActiveBookingLimitExceededException(MaxActiveBookingsPerUser);

        var booking = Booking.CreatePending(request.EventId, userId, request.Seats);
        await bookings.AddAsync(booking, cancellationToken);
        return Map(booking);
    }

    public async Task<BookingResponse> GetByIdAsync(
        Guid bookingId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var booking = await FindAsync(bookingId, cancellationToken);
        EnsureOwnerOrAdmin(booking, currentUserId, isAdmin);
        return Map(booking);
    }

    public async Task CancelAsync(
        Guid bookingId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var booking = await FindAsync(bookingId, cancellationToken);
        EnsureOwnerOrAdmin(booking, currentUserId, isAdmin);
        booking.Cancel(DateTimeOffset.UtcNow);
        await bookings.SaveChangesAsync(cancellationToken);
    }

    private async Task<Booking> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await bookings.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Booking {id} was not found.");

    private static void EnsureOwnerOrAdmin(Booking booking, Guid currentUserId, bool isAdmin)
    {
        if (!isAdmin && booking.UserId != currentUserId)
            throw new ForbiddenException("You can access only your own bookings.");
    }

    internal static BookingResponse Map(Booking booking) => new(
        booking.Id,
        booking.EventId,
        booking.UserId,
        booking.Seats,
        booking.Status,
        booking.CreatedAt,
        booking.ProcessedAt);
}
