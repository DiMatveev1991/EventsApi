using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsApi.Repositories
{
	/// <summary>
	/// Реализация <see cref="IBookingRepository"/> поверх <see cref="AppDbContext"/>.
	/// Содержит только логику доступа к данным — без бизнес-правил.
	/// </summary>
	public sealed class BookingRepository : IBookingRepository
	{
		private readonly AppDbContext _context;

		public BookingRepository(AppDbContext context)
		{
			_context = context;
		}

		public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		{
			return await _context.Bookings
				.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
		}

		public async Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default)
		{
			return await _context.Bookings
				.Where(b => b.Status == BookingStatus.Pending)
				.Select(b => b.Id)
				.ToListAsync(cancellationToken);
		}

		public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
		{
			// SaveChanges сохраняет и новую бронь, и любые другие изменения, отслеживаемые
			// тем же контекстом в текущем scope (например, уменьшение AvailableSeats
			// у события при бронировании) — одной транзакцией.
			_context.Bookings.Add(booking);
			await _context.SaveChangesAsync(cancellationToken);
		}

		public async Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default)
		{
			// Бронь (и, при необходимости, связанное событие) уже отслеживаются
			// контекстом — достаточно сохранить изменения.
			await _context.SaveChangesAsync(cancellationToken);
		}
	}
}
