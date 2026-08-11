using Bookings.Application.Abstractions;
using Bookings.Domain.Entities;
using Bookings.Domain.Enums;
using Bookings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bookings.Infrastructure.Repositories;

public sealed class BookingRepository(BookingsDbContext context) : IBookingRepository
{
    public Task<Booking?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        context.Bookings.SingleOrDefaultAsync(booking => booking.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetPendingIdsAsync(
        CancellationToken cancellationToken = default) =>
        await context.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Pending)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetUnpublishedConfirmedIdsAsync(
        CancellationToken cancellationToken = default) =>
        await context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.Confirmed &&
                booking.ConfirmationPublishedAt == null)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        context.Bookings.CountAsync(
            booking =>
                booking.UserId == userId &&
                (booking.Status == BookingStatus.Pending ||
                 booking.Status == BookingStatus.Confirmed),
            cancellationToken);

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        context.Bookings.Add(booking);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
