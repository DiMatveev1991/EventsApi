using System.Data;
using Contracts;
using EventsApi.Application.Abstractions;
using EventsApi.Application.Messaging;
using EventsApi.Domain.Entities;
using EventsApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventsApi.Infrastructure.Repositories
{
    /// <summary>
    /// Реализация порта <see cref="IEventRepository"/> поверх <see cref="AppDbContext"/>.
    /// Содержит только логику доступа к данным — без бизнес-правил.
    /// </summary>
    public sealed class EventRepository : IEventRepository
    {
        private readonly AppDbContext _context;

        /// <summary>Создаёт репозиторий поверх scoped-контекста Events.</summary>
        public EventRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>Возвращает детерминированно отсортированную страницу событий.</summary>
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

        /// <summary>Возвращает отслеживаемое событие по идентификатору.</summary>
        public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Events
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }

        /// <summary>Возвращает события с наибольшей долей проданных мест.</summary>
        public async Task<IReadOnlyList<Event>> GetTopPopularAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            if (count <= 0)
                return Array.Empty<Event>();

            // Деление выполняется в SQL до Take, поэтому из БД возвращается
            // только нужное количество уже отсортированных записей.
            return await _context.Events
                .AsNoTracking()
                .Where(e => e.TotalSeats > 0)
                .OrderByDescending(e =>
                    (double)(e.TotalSeats - e.AvailableSeats) / e.TotalSeats)
                .ThenBy(e => e.Id)
                .Take(count)
                .ToListAsync(cancellationToken);
        }

        /// <summary>Добавляет событие и фиксирует изменения в PostgreSQL.</summary>
        public async Task AddAsync(Event ev, CancellationToken cancellationToken = default)
        {
            _context.Events.Add(ev);
            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Фиксирует изменения отслеживаемого события.</summary>
        public async Task UpdateAsync(Event ev, CancellationToken cancellationToken = default)
        {
            // Сущность уже отслеживается контекстом — достаточно сохранить изменения.
            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Удаляет событие и фиксирует изменения в PostgreSQL.</summary>
        public async Task DeleteAsync(Event ev, CancellationToken cancellationToken = default)
        {
            _context.Events.Remove(ev);
            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Применяет подтверждение с ограниченными повторами конфликтов PostgreSQL.</summary>
        public async Task<BookingConfirmationResult> ApplyBookingConfirmedAsync(
            BookingConfirmed message,
            CancellationToken cancellationToken = default)
        {
            const int maxAttempts = 3;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await ApplyBookingConfirmedOnceAsync(message, cancellationToken);
                }
                catch (Exception exception) when (
                    attempt < maxAttempts && IsRetryableConcurrencyFailure(exception))
                {
                    // После rollback EF может хранить устаревшие состояния; очищаем
                    // tracker перед повтором Serializable-транзакции.
                    _context.ChangeTracker.Clear();
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
                }
            }
        }

        /// <summary>Атомарно применяет одно сообщение и записывает inbox-маркер.</summary>
        private async Task<BookingConfirmationResult> ApplyBookingConfirmedOnceAsync(
            BookingConfirmed message,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            // Проверка inbox и изменение мест входят в одну транзакцию, поэтому
            // повторная доставка не может уменьшить AvailableSeats второй раз.
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

            // Даже бизнес-отказ записывается в inbox: повтор того же неизменяемого
            // сообщения не должен бесконечно возвращаться из Kafka.
            _context.ProcessedBookingMessages.Add(
                ProcessedBookingMessage.Create(message.BookingId, result.ToString()));
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }

        /// <summary>Определяет, можно ли безопасно повторить транзакцию после конфликта.</summary>
        private static bool IsRetryableConcurrencyFailure(Exception exception)
        {
            var postgresException = exception as PostgresException
                ?? exception.InnerException as PostgresException;

            return postgresException?.SqlState is
                PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.DeadlockDetected or
                PostgresErrorCodes.UniqueViolation;
        }
    }
}
