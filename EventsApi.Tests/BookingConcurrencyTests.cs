using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Services;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

/// <summary>
/// Тесты на потокобезопасность <see cref="BookingService"/> при конкурентных запросах.
/// Используют реальный параллелизм (Task.Run + Task.WhenAll), а не последовательные вызовы.
/// </summary>
public class BookingConcurrencyTests
{
	private readonly IEventStore _eventStore;
	private readonly IEventService _eventService;
	private readonly IBookingStore _bookingStore;
	private readonly BookingService _sut;

	public BookingConcurrencyTests()
	{
		_eventStore = new InMemoryEventStore();
		_eventService = new EventService(_eventStore);
		_bookingStore = new InMemoryBookingStore();
		_sut = new BookingService(_bookingStore, _eventStore);
	}

	private EventDto CreateTestEvent(int totalSeats) =>
		_eventService.Create(TestData.CreateDto(totalSeats: totalSeats));

	[Fact]
	public async Task CreateBookingAsync_20ConcurrentRequestsFor5Seats_NoOverbooking()
	{
		// Arrange: событие на 5 мест, 20 конкурентных запросов.
		var ev = CreateTestEvent(totalSeats: 5);

		// Act: все запросы стартуют параллельно.
		var tasks = Enumerable.Range(0, 20)
			.Select(_ => Task.Run(async () =>
			{
				try
				{
					var booking = await _sut.CreateBookingAsync(ev.Id);
					return (Booking: (BookingDto?)booking, Error: (Exception?)null);
				}
				catch (NoAvailableSeatsException ex)
				{
					return (Booking: (BookingDto?)null, Error: (Exception?)ex);
				}
			}))
			.ToArray();

		var results = await Task.WhenAll(tasks);

		// Assert: ровно 5 успешных броней, 15 — NoAvailableSeatsException,
		// AvailableSeats = 0, лишних броней в хранилище нет.
		var succeeded = results.Where(r => r.Booking is not null).ToList();
		var failed = results.Where(r => r.Error is not null).ToList();

		succeeded.Should().HaveCount(5);
		failed.Should().HaveCount(15);
		failed.Should().OnlyContain(r => r.Error is NoAvailableSeatsException);

		succeeded.Select(r => r.Booking!.Id).Should().OnlyHaveUniqueItems();
		_eventService.GetById(ev.Id).AvailableSeats.Should().Be(0);
		_bookingStore.GetAll().Should().HaveCount(5);
	}

	[Fact]
	public async Task CreateBookingAsync_10ConcurrentRequestsFor10Seats_AllIdsUnique()
	{
		// Arrange: событие на 10 мест, 10 одновременных запросов.
		var ev = CreateTestEvent(totalSeats: 10);

		// Act
		var tasks = Enumerable.Range(0, 10)
			.Select(_ => Task.Run(() => _sut.CreateBookingAsync(ev.Id)))
			.ToArray();

		var bookings = await Task.WhenAll(tasks);

		// Assert: 10 броней, все Id уникальны, мест не осталось.
		bookings.Should().HaveCount(10);
		bookings.Select(b => b.Id).Should().OnlyHaveUniqueItems();
		bookings.Should().OnlyContain(b => b.Status == BookingStatus.Pending);
		_eventService.GetById(ev.Id).AvailableSeats.Should().Be(0);
		_bookingStore.GetAll().Should().HaveCount(10);
	}
}