using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Models;

namespace EventsApi.Services
{
    public class EventService : IEventService
    {
        private readonly List<Event> _events = new();
        private readonly object _lock = new();

        public PaginatedResult<EventDto> GetAll(EventQueryParameters query)
        {
            ArgumentNullException.ThrowIfNull(query);

            // Нормализуем пагинацию: защищаемся от отрицательных/нулевых значений.
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? 10 : query.PageSize;

            lock (_lock)
            {
                IEnumerable<Event> source = _events;

                // Фильтр по названию — регистронезависимое частичное совпадение.
                if (!string.IsNullOrWhiteSpace(query.Title))
                {
                    var title = query.Title.Trim();
                    source = source.Where(e =>
                        e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
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
                var filtered = source
                    .OrderBy(e => e.StartAt)
                    .ThenBy(e => e.Id)
                    .ToList();

                var totalCount = filtered.Count;

                var items = filtered
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(MapToDto)
                    .ToList();

                return new PaginatedResult<EventDto>(items, totalCount, page, pageSize);
            }
        }

        public EventDto GetById(Guid id)
        {
            lock (_lock)
            {
                var ev = _events.FirstOrDefault(e => e.Id == id)
                    ?? throw NotFoundException.ForEvent(id);
                return MapToDto(ev);
            }
        }

        public EventDto Create(CreateEventDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);
            ValidateWrite(dto);

            var ev = new Event
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                Description = dto.Description,
                StartAt = dto.StartAt,
                EndAt = dto.EndAt
            };

            lock (_lock)
            {
                _events.Add(ev);
            }

            return MapToDto(ev);
        }

        public EventDto Update(Guid id, UpdateEventDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);
            ValidateWrite(dto);

            lock (_lock)
            {
                var ev = _events.FirstOrDefault(e => e.Id == id)
                    ?? throw NotFoundException.ForEvent(id);

                ev.Title = dto.Title.Trim();
                ev.Description = dto.Description;
                ev.StartAt = dto.StartAt;
                ev.EndAt = dto.EndAt;

                return MapToDto(ev);
            }
        }

        public void Delete(Guid id)
        {
            lock (_lock)
            {
                var ev = _events.FirstOrDefault(e => e.Id == id)
                    ?? throw NotFoundException.ForEvent(id);
                _events.Remove(ev);
            }
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
            EndAt = ev.EndAt
        };
    }
}
