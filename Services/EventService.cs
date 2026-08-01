using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Models;
using EventsApi.Repositories;

namespace EventsApi.Services
{
	public class EventService : IEventService
	{
		private readonly IEventRepository _repository;

		public EventService(IEventRepository repository)
		{
			_repository = repository;
		}

		public async Task<PaginatedResult<EventDto>> GetAllAsync(
			EventQueryParameters query, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(query);

			// Нормализуем пагинацию: защищаемся от отрицательных/нулевых значений.
			var page = query.Page < 1 ? 1 : query.Page;
			var pageSize = query.PageSize < 1 ? 10 : query.PageSize;

			var (events, totalCount) = await _repository.GetPagedAsync(
				query.Title,
				query.From,
				query.To,
				(page - 1) * pageSize,
				pageSize,
				cancellationToken);

			var items = events.Select(MapToDto).ToList();

			return new PaginatedResult<EventDto>(items, totalCount, page, pageSize);
		}

		public async Task<EventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var ev = await _repository.GetByIdAsync(id, cancellationToken)
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

			await _repository.AddAsync(ev, cancellationToken);

			return MapToDto(ev);
		}

		public async Task<EventDto> UpdateAsync(Guid id, UpdateEventDto dto, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(dto);
			ValidateWrite(dto);

			var ev = await _repository.GetByIdAsync(id, cancellationToken)
				?? throw NotFoundException.ForEvent(id);

			ev.Title = dto.Title.Trim();
			ev.Description = dto.Description;
			ev.StartAt = dto.StartAt;
			ev.EndAt = dto.EndAt;

			await _repository.UpdateAsync(ev, cancellationToken);

			return MapToDto(ev);
		}

		public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var ev = await _repository.GetByIdAsync(id, cancellationToken)
				?? throw NotFoundException.ForEvent(id);

			await _repository.DeleteAsync(ev, cancellationToken);
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