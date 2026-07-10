using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsApi.Services
{
	public class EventService : IEventService
	{
		private readonly AppDbContext _context;

		public EventService(AppDbContext context)
		{
			_context = context;
		}

		public async Task<PaginatedResult<EventDto>> GetAllAsync(
			EventQueryParameters query, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(query);

			// Нормализуем пагинацию: защищаемся от отрицательных/нулевых значений.
			var page = query.Page < 1 ? 1 : query.Page;
			var pageSize = query.PageSize < 1 ? 10 : query.PageSize;

			IQueryable<Event> source = _context.Events.AsNoTracking();

			// Фильтр по названию — регистронезависимое частичное совпадение.
			if (!string.IsNullOrWhiteSpace(query.Title))
			{
				var title = query.Title.Trim().ToLower();
				source = source.Where(e => e.Title.ToLower().Contains(title));
			}

			// from: событие должно НАЧИНАТЬСЯ не раньше указанной даты.
			if (query.From.HasValue)
			{
				var from = query.From.Value;
				source = source.Where(e => e.StartAt >= from);
			}

			// to: событие должно ЗАКАНЧИВАТЬСЯ не позже указанной даты.
			if (query.To.HasValue)
			{
				var to = query.To.Value;
				source = source.Where(e => e.EndAt <= to);
			}

			// Стабильная сортировка, чтобы пагинация была детерминированной.
			var ordered = source
				.OrderBy(e => e.StartAt)
				.ThenBy(e => e.Id);

			var totalCount = await ordered.CountAsync(cancellationToken);

			var events = await ordered
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync(cancellationToken);

			var items = events.Select(MapToDto).ToList();

			return new PaginatedResult<EventDto>(items, totalCount, page, pageSize);
		}

		public async Task<EventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var ev = await _context.Events
				.AsNoTracking()
				.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
				?? throw NotFoundException.ForEvent(id);
			return MapToDto(ev);
		}

		public async Task<EventDto> CreateAsync(CreateEventDto dto, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(dto);
			ValidateWrite(dto);

			if (dto.TotalSeats is null)
				throw new ValidationException(
					"Некорректные данные мероприятия",
					new Dictionary<string, string[]>
					{
						[nameof(dto.TotalSeats)] = new[] { "Поле TotalSeats обязательно" }
					});

			// Фабричный метод валидирует totalSeats (> 0) и устанавливает
			// AvailableSeats = TotalSeats.
			var ev = Event.Create(
				dto.Title.Trim(),
				dto.Description,
				dto.StartAt,
				dto.EndAt,
				dto.TotalSeats.Value);

			_context.Events.Add(ev);
			await _context.SaveChangesAsync(cancellationToken);

			return MapToDto(ev);
		}

		public async Task<EventDto> UpdateAsync(Guid id, UpdateEventDto dto, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(dto);
			ValidateWrite(dto);

			var ev = await _context.Events
				.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
				?? throw NotFoundException.ForEvent(id);

			ev.Title = dto.Title.Trim();
			ev.Description = dto.Description;
			ev.StartAt = dto.StartAt;
			ev.EndAt = dto.EndAt;

			await _context.SaveChangesAsync(cancellationToken);

			return MapToDto(ev);
		}

		public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var ev = await _context.Events
				.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
				?? throw NotFoundException.ForEvent(id);

			_context.Events.Remove(ev);
			await _context.SaveChangesAsync(cancellationToken);
		}

		/// <summary>
		/// Валидация на уровне сервиса — защита от вызовов в обход контроллера
		/// (в т. ч. из тестов). ModelState в контроллере тоже продолжает работать.
		/// </summary>
		private static void ValidateWrite(EventWriteDto dto)
		{
			var errors = new Dictionary<string, List<string>>();

			if (string.IsNullOrWhiteSpace(dto.Title))
				AddError(errors, nameof(dto.Title), "Title не может быть пустым");

			if (dto.EndAt <= dto.StartAt)
				AddError(errors, nameof(dto.EndAt), "EndAt должен быть позже StartAt");

			if (errors.Count > 0)
			{
				var dict = errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
				throw new ValidationException("Некорректные данные мероприятия", dict);
			}
		}

		private static void AddError(Dictionary<string, List<string>> bag, string key, string msg)
		{
			if (!bag.TryGetValue(key, out var list))
			{
				list = new List<string>();
				bag[key] = list;
			}
			list.Add(msg);
		}

		private static EventDto MapToDto(Event ev) => new()
		{
			Id = ev.Id,
			Title = ev.Title,
			Description = ev.Description,
			StartAt = ev.StartAt,
			EndAt = ev.EndAt,
			TotalSeats = ev.TotalSeats,
			AvailableSeats = ev.AvailableSeats
		};
	}
}