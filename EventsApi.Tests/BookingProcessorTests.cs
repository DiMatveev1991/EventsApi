using EventsApi.BackgroundServices;
using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Models;
using EventsApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventsApi.Tests;

public class BookingProcessorTests
{
	/// <summary>
	/// Время ожидания обработки. Сервис делает Task.Delay(2 сек) на каждую бронь
	/// и опрашивает раз в 500 мс. 10 секунд — с большим запасом для CI.
	/// </summary>
	private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(10);

	private static (IEventService events, IBookingStore store, BookingProcessor processor)
		BuildSut()
	{
		IEventService events = new EventService();
		IBookingStore store = new InMemoryBookingStore();
		var processor = new BookingProcessor(store, events, NullLogger<BookingProcessor>.Instance);
		return (events, store, processor);
	}

	private static async Task<Booking> WaitUntilProcessedAsync(IBookingStore store, Guid bookingId)
	{
		var deadline = DateTime.UtcNow + MaxWait;
		while (DateTime.UtcNow < deadline)
		{
			var booking = store.GetById(bookingId);
			if (booking is not null && booking.Status != BookingStatus.Pending)
				return booking;

			await Task.Delay(100);
		}

		throw new TimeoutException(
			$"Бронь {bookingId} не была обработана за {MaxWait.TotalSeconds} сек");
	}

	[Fact]
	public async Task ProcessesPendingBooking_ToConfirmed_WhenEventExists()
	{
		// Arrange
		var (events, store, processor) = BuildSut();
		var ev = events.Create(TestData.CreateDto());
		var booking = Booking.CreatePending(ev.Id);
		store.Add(booking);

		// Act
		await processor.StartAsync(CancellationToken.None);
		var processed = await WaitUntilProcessedAsync(store, booking.Id);
		await processor.StopAsync(CancellationToken.None);

		// Assert
		processed.Status.Should().Be(BookingStatus.Confirmed);
		processed.ProcessedAt.Should().NotBeNull();
		processed.ProcessedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(15));
	}

	[Fact]
	public async Task ProcessesPendingBooking_ToRejected_WhenEventDeleted()
	{
		// Arrange
		var (events, store, processor) = BuildSut();
		var ev = events.Create(TestData.CreateDto());
		var booking = Booking.CreatePending(ev.Id);
		store.Add(booking);

		// Удаляем событие до того, как фоновый сервис до него доберётся.
		events.Delete(ev.Id);

		// Act
		await processor.StartAsync(CancellationToken.None);
		var processed = await WaitUntilProcessedAsync(store, booking.Id);
		await processor.StopAsync(CancellationToken.None);

		// Assert
		processed.Status.Should().Be(BookingStatus.Rejected);
		processed.ProcessedAt.Should().NotBeNull();
	}

	[Fact]
	public async Task ProcessesMultiplePendingBookings()
	{
		// Arrange
		var (events, store, processor) = BuildSut();
		var ev = events.Create(TestData.CreateDto());

		var b1 = Booking.CreatePending(ev.Id);
		var b2 = Booking.CreatePending(ev.Id);
		var b3 = Booking.CreatePending(ev.Id);
		store.Add(b1);
		store.Add(b2);
		store.Add(b3);

		// Act
		await processor.StartAsync(CancellationToken.None);

		// Все три брони должны быть обработаны (по 2 сек каждая).
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
		while (DateTime.UtcNow < deadline)
		{
			if (store.GetPending().Count == 0) break;
			await Task.Delay(200);
		}

		await processor.StopAsync(CancellationToken.None);

		// Assert
		store.GetPending().Should().BeEmpty();
		store.GetAll().Should().OnlyContain(b => b.Status == BookingStatus.Confirmed);
	}

	[Fact]
	public async Task StopAsync_CancelsGracefully_WithoutThrowing()
	{
		// Arrange
		var (_, _, processor) = BuildSut();

		// Act
		await processor.StartAsync(CancellationToken.None);
		await Task.Delay(100);
		var act = async () => await processor.StopAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task DoesNothing_WhenNoPendingBookings()
	{
		// Arrange
		var (_, store, processor) = BuildSut();

		// Act
		await processor.StartAsync(CancellationToken.None);
		await Task.Delay(1500); // даём сервису поработать на пустом сторе
		await processor.StopAsync(CancellationToken.None);

		// Assert
		store.GetAll().Should().BeEmpty();
	}
}