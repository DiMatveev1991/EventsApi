using System.Data;
using Contracts;
using EventsApi.Application.Abstractions;
using EventsApi.Application.Messaging;
using EventsApi.Domain.Entities;
using EventsApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventsApi.Infrastructure.Repositories
{
    /// <summary>
    /// Реализация порта <see cref="IEventRepository"/> поверх <see cref="AppDbContext"/>.
    /// Содержит только логику доступа к данным — без бизнес-правил.
    /// </summary>
    public sealed class EventRepository : IEventRepository
    {
        private readonly AppDbContext _context;

        public EventRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
            string? title,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            // Чтение только для выборки — трекинг не нужен.
            IQueryable<Event> source = _context.Events.AsNoTracking();

            // Фильтр по названию — регистронезависимое частичное совпадение.
            if (!string.IsNullOrWhiteSpace(title))
            {
                var normalized = title.Trim().ToLower();
                source = source.Where(e => e.Title.ToLower().Contains(normalized));
            }

            // from: событие должно НАЧИНАТЬСЯ не раньше указанной даты.
            if (from.HasValue)
            {
                var fromValue = from.Value;
                source = source.Where(e => e.StartAt >= fromValue);
            }

            // to: событие должно ЗАКАНЧИВАТЬСЯ не позже указанной даты.
            if (to.HasValue)
            {
                var toValue = to.Value;
                source = source.Where(e => e.EndAt <= toValue);
            }

            // Стабильная сортировка, чтобы пагинация была детерминированной.
            var ordered = source
                .OrderBy(e => e.StartAt)
                .ThenBy(e => e.Id);

            var totalCount = await ordered.CountAsync(cancellationToken);

            var items = await ordered
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Events
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }

        public async Task AddAsync(Event ev, CancellationToken cancellationToken = default)
        {
            _context.Events.Add(ev);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(Event ev, CancellationToken cancellationToken = default)
        {
            // Сущность уже отслеживается контекстом — достаточно сохранить изменения.
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Event ev, CancellationToken cancellationToken = default)
        {
            _context.Events.Remove(ev);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<BookingConfirmationResult> ApplyBookingConfirmedAsync(
            BookingConfirmed message,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            if (await _context.ProcessedBookingMessages.AnyAsync(
                    processed => processed.BookingId == message.BookingId,
                    cancellationToken))
            {
                return BookingConfirmationResult.Duplicate;
            }

            var ev = await _context.Events.SingleOrDefaultAsync(
                item => item.Id == message.EventId,
                cancellationToken);

            BookingConfirmationResult result;
            if (ev is null)
                result = BookingConfirmationResult.EventNotFound;
            else if (!ev.TryReserveSeats(message.Seats))
                result = BookingConfirmationResult.InsufficientSeats;
            else
                result = BookingConfirmationResult.Applied;

            _context.ProcessedBookingMessages.Add(
                ProcessedBookingMessage.Create(message.BookingId, result.ToString()));
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
    }
}
