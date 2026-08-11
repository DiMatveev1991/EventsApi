using Bookings.Domain.Entities;

namespace Bookings.Application.Abstractions;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetUnpublishedConfirmedIdsAsync(
        CancellationToken cancellationToken = default);
    Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
