using Contracts;
using EventsApi.Application.Abstractions;
using EventsApi.Application.Caching;
using EventsApi.Application.Dtos;
using EventsApi.Application.Messaging;
using EventsApi.Application.Services;
using EventsApi.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventsApi.Tests;

public sealed class EventCachingTests
{
    private static readonly CacheOptions OptionsValue = new()
    {
        EventTtlSeconds = 120,
        TopEventsTtlSeconds = 30
    };

    [Fact]
    public async Task GetById_cache_hit_does_not_call_repository()
    {
        var id = Guid.NewGuid();
        var repository = new RecordingEventRepository();
        var cache = new RecordingCache();
        cache.Seed(CacheKeys.Event(id), Dto(id, "Cached"));
        var service = CreateService(repository, cache);

        var result = await service.GetByIdAsync(id);

        result.Title.Should().Be("Cached");
        repository.GetByIdCalls.Should().Be(0);
    }

    [Fact]
    public async Task GetById_cache_miss_reads_repository_and_populates_cache()
    {
        var ev = CreateEvent("Database");
        var repository = new RecordingEventRepository { EventById = ev };
        var cache = new RecordingCache();
        var service = CreateService(repository, cache);

        var result = await service.GetByIdAsync(ev.Id);

        result.Title.Should().Be("Database");
        repository.GetByIdCalls.Should().Be(1);
        cache.SetKeys.Should().ContainSingle().Which.Should().Be(CacheKeys.Event(ev.Id));
        cache.LastTimeToLive.Should().Be(TimeSpan.FromSeconds(120));
        cache.Get<EventDto>(CacheKeys.Event(ev.Id))!.Id.Should().Be(ev.Id);
    }

    [Fact]
    public async Task Top_cache_hit_does_not_call_repository()
    {
        var repository = new RecordingEventRepository();
        var cache = new RecordingCache();
        cache.Seed(CacheKeys.TopEvents, new List<PopularEventDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Cached top" }
        });
        var service = CreateService(repository, cache);

        var result = await service.GetTopPopularAsync();

        result.Should().ContainSingle().Which.Title.Should().Be("Cached top");
        repository.GetTopCalls.Should().Be(0);
    }

    [Fact]
    public async Task Top_cache_miss_reads_ten_events_and_uses_own_ttl()
    {
        var repository = new RecordingEventRepository
        {
            TopEvents = new[] { CreateEvent("Popular", totalSeats: 8, availableSeats: 2) }
        };
        var cache = new RecordingCache();
        var service = CreateService(repository, cache);

        var result = await service.GetTopPopularAsync();

        repository.GetTopCalls.Should().Be(1);
        repository.RequestedTopCount.Should().Be(10);
        result.Should().ContainSingle().Which.SoldPercentage.Should().Be(75);
        cache.SetKeys.Should().ContainSingle().Which.Should().Be(CacheKeys.TopEvents);
        cache.LastTimeToLive.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Create_invalidates_event_key_after_database_commit()
    {
        var order = new List<string>();
        var repository = new RecordingEventRepository(order);
        var cache = new RecordingCache(order);
        var service = CreateService(repository, cache);

        var created = await service.CreateAsync(TestData.CreateEvent());

        cache.RemovedKeys.Should().ContainSingle().Which.Should().Be(CacheKeys.Event(created.Id));
        order.Should().ContainInOrder("database:add", "cache:remove");
        cache.RemovedKeys.Should().NotContain(CacheKeys.TopEvents);
    }

    [Fact]
    public async Task Update_invalidates_event_key_after_database_commit()
    {
        var order = new List<string>();
        var ev = CreateEvent("Before");
        var repository = new RecordingEventRepository(order) { EventById = ev };
        var cache = new RecordingCache(order);
        cache.Seed(CacheKeys.Event(ev.Id), Dto(ev.Id, "Before"));
        var service = CreateService(repository, cache);

        await service.UpdateAsync(ev.Id, TestData.UpdateEvent(title: "After"));

        cache.RemovedKeys.Should().ContainSingle().Which.Should().Be(CacheKeys.Event(ev.Id));
        order.Should().ContainInOrder("database:update", "cache:remove");
        cache.Get<EventDto>(CacheKeys.Event(ev.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_invalidates_event_key_after_database_commit()
    {
        var order = new List<string>();
        var ev = CreateEvent("Delete");
        var repository = new RecordingEventRepository(order) { EventById = ev };
        var cache = new RecordingCache(order);
        cache.Seed(CacheKeys.Event(ev.Id), Dto(ev.Id, ev.Title));
        var service = CreateService(repository, cache);

        await service.DeleteAsync(ev.Id);

        cache.RemovedKeys.Should().ContainSingle().Which.Should().Be(CacheKeys.Event(ev.Id));
        order.Should().ContainInOrder("database:delete", "cache:remove");
    }

    [Fact]
    public async Task Kafka_applied_message_invalidates_event_after_database_commit()
    {
        var order = new List<string>();
        var repository = new RecordingEventRepository(order)
        {
            BookingResult = BookingConfirmationResult.Applied
        };
        var cache = new RecordingCache(order);
        var handler = new BookingConfirmedHandler(repository, cache);
        var eventId = Guid.NewGuid();

        var result = await handler.HandleAsync(Message(eventId));

        result.Should().Be(BookingConfirmationResult.Applied);
        cache.RemovedKeys.Should().ContainSingle().Which.Should().Be(CacheKeys.Event(eventId));
        order.Should().ContainInOrder("database:kafka", "cache:remove");
    }

    [Fact]
    public async Task Kafka_duplicate_message_does_not_invalidate_cache()
    {
        var repository = new RecordingEventRepository
        {
            BookingResult = BookingConfirmationResult.Duplicate
        };
        var cache = new RecordingCache();
        var handler = new BookingConfirmedHandler(repository, cache);

        await handler.HandleAsync(Message(Guid.NewGuid()));

        cache.RemovedKeys.Should().BeEmpty();
    }

    private static EventService CreateService(
        IEventRepository repository,
        ICacheService cache) =>
        new(repository, cache, Options.Create(OptionsValue));

    private static Event CreateEvent(
        string title,
        int totalSeats = 10,
        int? availableSeats = null)
    {
        var ev = Event.Create(
            title,
            null,
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(2),
            totalSeats);
        if (availableSeats.HasValue)
            ev.AvailableSeats = availableSeats.Value;
        return ev;
    }

    private static EventDto Dto(Guid id, string title) => new()
    {
        Id = id,
        Title = title
    };

    private static BookingConfirmed Message(Guid eventId) => new(
        Guid.NewGuid(),
        eventId,
        Guid.NewGuid(),
        1,
        DateTimeOffset.UtcNow);

    private sealed class RecordingEventRepository : IEventRepository
    {
        private readonly List<string>? _order;

        public RecordingEventRepository(List<string>? order = null) => _order = order;

        public Event? EventById { get; set; }
        public IReadOnlyList<Event> TopEvents { get; set; } = Array.Empty<Event>();
        public BookingConfirmationResult BookingResult { get; set; } = BookingConfirmationResult.Applied;
        public int GetByIdCalls { get; private set; }
        public int GetTopCalls { get; private set; }
        public int RequestedTopCount { get; private set; }

        public Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
            string? title,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int skip,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<Event>, int)>((Array.Empty<Event>(), 0));

        public Task<Event?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            GetByIdCalls++;
            return Task.FromResult(EventById);
        }

        public Task<IReadOnlyList<Event>> GetTopPopularAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            GetTopCalls++;
            RequestedTopCount = count;
            return Task.FromResult(TopEvents);
        }

        public Task AddAsync(Event ev, CancellationToken cancellationToken = default)
        {
            EventById = ev;
            _order?.Add("database:add");
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Event ev, CancellationToken cancellationToken = default)
        {
            _order?.Add("database:update");
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Event ev, CancellationToken cancellationToken = default)
        {
            EventById = null;
            _order?.Add("database:delete");
            return Task.CompletedTask;
        }

        public Task<BookingConfirmationResult> ApplyBookingConfirmedAsync(
            BookingConfirmed message,
            CancellationToken cancellationToken = default)
        {
            _order?.Add("database:kafka");
            return Task.FromResult(BookingResult);
        }
    }

    private sealed class RecordingCache : ICacheService
    {
        private readonly Dictionary<string, object> _values = new();
        private readonly List<string>? _order;

        public RecordingCache(List<string>? order = null) => _order = order;

        public List<string> SetKeys { get; } = new();
        public List<string> RemovedKeys { get; } = new();
        public TimeSpan? LastTimeToLive { get; private set; }

        public void Seed<T>(string key, T value) where T : class => _values[key] = value;

        public T? Get<T>(string key) where T : class =>
            _values.TryGetValue(key, out var value) ? value as T : null;

        public Task<T?> GetAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
            where T : class => Task.FromResult(Get<T>(key));

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default)
            where T : class
        {
            _values[key] = value;
            SetKeys.Add(key);
            LastTimeToLive = timeToLive;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            RemovedKeys.Add(key);
            _order?.Add("cache:remove");
            return Task.CompletedTask;
        }
    }
}
