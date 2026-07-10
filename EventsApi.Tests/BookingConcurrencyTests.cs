using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.Tests;

/// <summary>
/// Тесты на потокобезопасность <see cref="BookingService"/> при конкурентных запросах.
/// Используют реальный параллелизм (Task.Run + Task.WhenAll): для каждого параллельного
/// запроса создаётся отдельный scope со своим DbContext, а критическую секцию защищает
/// статический семафор внутри сервиса.
/// </summary>
public class BookingConcurrencyTests : IDisposable
{
	private readonly ServiceProvider _serviceProvider;

	public BookingConcurrencyTests()
	{
		_serviceProvider = TestHost.Build();
	}

	public void Dispose() => _serviceProvider.Dispose();

	private async Task<EventDto> CreateTestEvent(int totalSeats)
	{
		using var scope = _serviceProvider.CreateScope();
		var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
		return await eventService.CreateAsync(TestData.CreateDto(totalSeats: totalSeats));
	}

	private async Task<int> GetAvailableSeats(Guid eventId)
	{
		using var scope = _serviceProvider.CreateScope();
		var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
		return (await eventService.GetByIdAsync(eventId)).AvailableSeats;
	}

	private async Task<int> GetBookingsCount()
	{
		using var scope = _serviceProvider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		return await db.Bookings.CountAsync();
	}

	[Fact]
	public async Task CreateBookingAsync_20ConcurrentRequestsFor5Seats_NoOverbooking()
	{
		// Arrange: событие на 5 мест, 20 конкурентных запросов.
		var ev = await CreateTestEvent(totalSeats: 5);

		// Act: все запросы стартуют параллельно, каждый — в своём scope.
		var tasks = Enumerable.Range(0, 20)
			.Select(_ => Task.Run(async () =>
			{
				using var scope = _serviceProvider.CreateScope();
				var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
				try
				{
					var booking = await bookingService.CreateBookingAsync(ev.Id);
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
		// AvailableSeats = 0, лишних броней в БД нет.
		var succeeded = results.Where(r => r.Booking is not null).ToList();
		var failed = results.Where(r => r.Error is not null).ToList();

		succeeded.Should().HaveCount(5);
		failed.Should().HaveCount(15);
		failed.Should().OnlyContain(r => r.Error is NoAvailableSeatsException);

		succeeded.Select(r => r.Booking!.Id).Should().OnlyHaveUniqueItems();
		(await GetAvailableSeats(ev.Id)).Should().Be(0);
		(await GetBookingsCount()).Should().Be(5);
	}

	[Fact]
	public async Task CreateBookingAsync_10ConcurrentRequestsFor10Seats_AllIdsUnique()
	{
		// Arrange: событие на 10 мест, 10 одновременных запросов.
		var ev = await CreateTestEvent(totalSeats: 10);

		// Act
		var tasks = Enumerable.Range(0, 10)
			.Select(_ => Task.Run(async () =>
			{
				using var scope = _serviceProvider.CreateScope();
				var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
				return await bookingService.CreateBookingAsync(ev.Id);
			}))
			.ToArray();

		var bookings = await Task.WhenAll(tasks);

		// Assert: 10 броней, все Id уникальны, мест не осталось.
		bookings.Should().HaveCount(10);
		bookings.Select(b => b.Id).Should().OnlyHaveUniqueItems();
		bookings.Should().OnlyContain(b => b.Status == BookingStatus.Pending);
		(await GetAvailableSeats(ev.Id)).Should().Be(0);
		(await GetBookingsCount()).Should().Be(10);
	}
}
