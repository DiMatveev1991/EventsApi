using EventsApi.Application.Abstractions;
using EventsApi.Application.Caching;
using EventsApi.Application.Dtos;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace EventsApi.Application.Services
{
    /// <summary>
    /// Реализует прикладные сценарии Events и координирует PostgreSQL с кешем.
    /// </summary>
    public class EventService : IEventService
    {
        private readonly IEventRepository _repository;
        private readonly ICacheService _cache;
        private readonly CacheOptions _cacheOptions;

        /// <summary>Создаёт сервис событий с репозиторием и абстракцией кеша.</summary>
        public EventService(
            IEventRepository repository,
            ICacheService cache,
            IOptions<CacheOptions> cacheOptions)
        {
            _repository = repository;
            _cache = cache;
            _cacheOptions = cacheOptions.Value;
        }

        /// <summary>Возвращает страницу событий с фильтрацией и нормализованной пагинацией.</summary>
        public async Task<PaginatedResult<EventDto>> GetAllAsync(
            EventQueryParameters query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            // Нормализуем пагинацию: защищаемся от отрицательных/нулевых значений.
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? 10 : query.PageSize;

            var (events, totalCount) = await _repository.GetPagedAsync(
                query.Title,
                query.From?.ToUniversalTime(),
                query.To?.ToUniversalTime(),
                (page - 1) * pageSize,
                pageSize,
                cancellationToken);

            var items = events.Select(MapToDto).ToList();

            return new PaginatedResult<EventDto>(items, totalCount, page, pageSize);
        }

        /// <summary>Возвращает событие по идентификатору по паттерну Cache-Aside.</summary>
        public async Task<EventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var key = CacheKeys.Event(id);
            var cached = await _cache.GetAsync<EventDto>(key, cancellationToken);
            if (cached is not null)
                return cached;

            // Redis не является источником истины: промах или сбой переводит чтение в PostgreSQL.
            var ev = await _repository.GetByIdAsync(id, cancellationToken)
                ?? throw NotFoundException.ForEvent(id);

            var result = MapToDto(ev);
            await _cache.SetAsync(key, result, _cacheOptions.EventTtl, cancellationToken);
            return result;
        }

        /// <summary>Возвращает кешируемый по TTL рейтинг из десяти популярных событий.</summary>
        public async Task<IReadOnlyList<PopularEventDto>> GetTopPopularAsync(
            CancellationToken cancellationToken = default)
        {
            var cached = await _cache.GetAsync<List<PopularEventDto>>(
                CacheKeys.TopEvents,
                cancellationToken);
            if (cached is not null)
                return cached;

            // Топ — допустимо слегка устаревающий агрегат, поэтому он обновляется
            // только по TTL и не инвалидируется при каждом бронировании.
            var events = await _repository.GetTopPopularAsync(10, cancellationToken);
            var result = events.Select(MapToPopularDto).ToList();
            await _cache.SetAsync(
                CacheKeys.TopEvents,
                result,
                _cacheOptions.TopEventsTtl,
                cancellationToken);
            return result;
        }

        /// <summary>Создаёт событие и после фиксации в БД инвалидирует его ключ кеша.</summary>
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

            // Сначала фиксируем запись в БД, затем удаляем потенциально устаревший ключ.
            await _cache.RemoveAsync(CacheKeys.Event(ev.Id), cancellationToken);

            return MapToDto(ev);
        }

        /// <summary>Обновляет событие и после фиксации в БД инвалидирует его кеш.</summary>
        public async Task<EventDto> UpdateAsync(Guid id, UpdateEventDto dto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            ValidateWrite(dto);

            var ev = await _repository.GetByIdAsync(id, cancellationToken)
                ?? throw NotFoundException.ForEvent(id);

            ev.Title = dto.Title.Trim();
            ev.Description = dto.Description;
            ev.StartAt = dto.StartAt.ToUniversalTime();
            ev.EndAt = dto.EndAt.ToUniversalTime();

            await _repository.UpdateAsync(ev, cancellationToken);

            // Cache-Aside с инвалидацией при записи: следующий GET прогреет кеш.
            await _cache.RemoveAsync(CacheKeys.Event(id), cancellationToken);

            return MapToDto(ev);
        }

        /// <summary>Удаляет событие и после фиксации в БД удаляет его ключ кеша.</summary>
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var ev = await _repository.GetByIdAsync(id, cancellationToken)
                ?? throw NotFoundException.ForEvent(id);

            await _repository.DeleteAsync(ev, cancellationToken);
            await _cache.RemoveAsync(CacheKeys.Event(id), cancellationToken);
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

        /// <summary>Добавляет сообщение в коллекцию ошибок заданного поля.</summary>
        private static void AddError(Dictionary<string, List<string>> bag, string key, string msg)
        {
            if (!bag.TryGetValue(key, out var list))
            {
                list = new List<string>();
                bag[key] = list;
            }
            list.Add(msg);
        }

        /// <summary>Преобразует доменную сущность в DTO API.</summary>
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

        /// <summary>Преобразует событие в элемент рейтинга и рассчитывает процент продаж.</summary>
        private static PopularEventDto MapToPopularDto(Event ev) => new()
        {
            Id = ev.Id,
            Title = ev.Title,
            Description = ev.Description,
            StartAt = ev.StartAt,
            EndAt = ev.EndAt,
            TotalSeats = ev.TotalSeats,
            AvailableSeats = ev.AvailableSeats,
            SoldPercentage = ev.TotalSeats <= 0
                ? 0
                : Math.Round(
                    (double)(ev.TotalSeats - ev.AvailableSeats) / ev.TotalSeats * 100,
                    2,
                    MidpointRounding.AwayFromZero)
        };
    }
}
