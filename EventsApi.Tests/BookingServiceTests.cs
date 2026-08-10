using EventsApi.Application.Abstractions;
using EventsApi.Application.Services;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Enums;
using EventsApi.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

/// <summary>
/// Изолированные unit-тесты BookingService. Хранилище представлено тестовыми
/// реализациями портов; EF Core и AppDbContext здесь не участвуют.
/// </summary>
public sealed class BookingServiceTests
{
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly BookingService _sut;

    public BookingServiceTests()
    {
        _sut = new BookingService(_events, _bookings, new EventBookingLock());
    }

    [Fact]
    public async Task CreateBookingAsync_ForExistingEvent_ReturnsPendingBooking()
    {
        var ev = AddEvent();

        var booking = await _sut.CreateBookingAsync(ev.Id);

        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(ev.Id);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        booking.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_PersistsBookingThroughRepositoryPort()
    {
        var ev = AddEvent();

        var created = await _sut.CreateBookingAsync(ev.Id);

        (await _bookings.GetByIdAsync(created.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_DecreasesAvailableSeats()
    {
        var ev = AddEvent(totalSeats: 3);

        await _sut.CreateBookingAsync(ev.Id);
        await _sut.CreateBookingAsync(ev.Id);

        ev.AvailableSeats.Should().Be(1);
    }

    [Fact]
    public async Task CreateBookingAsync_UpToLimit_CreatesUniqueBookings()
    {
        var ev = AddEvent(totalSeats: 3);

        var bookings = new[]
        {
            await _sut.CreateBookingAsync(ev.Id),
            await _sut.CreateBookingAsync(ev.Id),
            await _sut.CreateBookingAsync(ev.Id)
        };

        bookings.Select(booking => booking.Id).Should().OnlyHaveUniqueItems();
        bookings.Should().OnlyContain(booking => booking.Status == BookingStatus.Pending);
        ev.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ExistingBooking_ReturnsCurrentState()
    {
        var ev = AddEvent();
        var created = await _sut.CreateBookingAsync(ev.Id);
        var entity = await _bookings.GetByIdAsync(created.Id);
        entity!.Confirm(DateTime.UtcNow);

        var result = await _sut.GetBookingByIdAsync(created.Id);

        result.Status.Should().Be(BookingStatus.Confirmed);
        result.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ReleaseSeats_AllowsNewBookingForSameSeat()
    {
        var ev = AddEvent(totalSeats: 1);
        var first = await _sut.CreateBookingAsync(ev.Id);
        ev.ReleaseSeats();

        var second = await _sut.CreateBookingAsync(ev.Id);

        second.Id.Should().NotBe(first.Id);
        ev.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBookingAsync_ForNonExistentEvent_ThrowsNotFound()
    {
        var unknownEventId = Guid.NewGuid();

        var act = async () => await _sut.CreateBookingAsync(unknownEventId);

        await act.Should()
            .ThrowAsync<NotFoundException>()
            .Where(exception => exception.StatusCode == 404)
            .Where(exception => exception.Message.Contains(unknownEventId.ToString()));
    }

    [Fact]
    public async Task CreateBookingAsync_ForDeletedEvent_ThrowsNotFound()
    {
        var ev = AddEvent();
        await _events.DeleteAsync(ev);

        var act = async () => await _sut.CreateBookingAsync(ev.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateBookingAsync_WhenSeatsExhausted_ThrowsAndDoesNotPersist()
    {
        var ev = AddEvent(totalSeats: 1);
        await _sut.CreateBookingAsync(ev.Id);

        var act = async () => await _sut.CreateBookingAsync(ev.Id);

        await act.Should()
            .ThrowAsync<NoAvailableSeatsException>()
            .Where(exception => exception.StatusCode == 409);
        _bookings.Count.Should().Be(1);
        ev.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task GetBookingByIdAsync_NonExistentId_ThrowsNotFound()
    {
        var unknownBookingId = Guid.NewGuid();

        var act = async () => await _sut.GetBookingByIdAsync(unknownBookingId);

        await act.Should()
            .ThrowAsync<NotFoundException>()
            .Where(exception => exception.Message.Contains(unknownBookingId.ToString()));
    }

    private Event AddEvent(int totalSeats = 10)
    {
        var ev = Event.Create(
            "Test event",
            null,
            new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero),
            totalSeats);
        _events.Add(ev);
        return ev;
    }

    private sealed class InMemoryEventRepository : IEventRepository
    {
        private readonly Dictionary<Guid, Event> _items = new();

        public void Add(Event ev) => _items.Add(ev.Id, ev);

        public Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
            string? title,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Event> items = _items.Values.Skip(skip).Take(take).ToList();
            return Task.FromResult((items, _items.Count));
        }

        public Task<Event?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault(id));

        public Task AddAsync(Event ev, CancellationToken cancellationToken = default)
        {
            Add(ev);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Event ev, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Event ev, CancellationToken cancellationToken = default)
        {
            _items.Remove(ev.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryBookingRepository : IBookingRepository
    {
        private readonly Dictionary<Guid, Booking> _items = new();

        public int Count => _items.Count;

        public Task<Booking?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault(id));

        public Task<IReadOnlyList<Guid>> GetPendingIdsAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Guid> ids = _items.Values
                .Where(booking => booking.Status == BookingStatus.Pending)
                .Select(booking => booking.Id)
                .ToList();
            return Task.FromResult(ids);
        }

        public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            _items.Add(booking.Id, booking);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
