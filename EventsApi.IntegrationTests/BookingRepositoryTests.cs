using EventsApi.DTOs;
using EventsApi.IntegrationTests.Infrastructure;
using EventsApi.Models;
using EventsApi.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventsApi.IntegrationTests
{
	/// <summary>
	/// Интеграционные тесты <see cref="BookingRepository"/> на реальной PostgreSQL:
	/// проверяют все методы репозитория, а также совместную с событием транзакцию
	/// и ограничение внешнего ключа.
	/// </summary>
	public sealed class BookingRepositoryTests : IntegrationTestBase
	{
		public BookingRepositoryTests(PostgresDatabaseFixture fixture) : base(fixture) { }

		[Fact]
		public async Task AddAsync_persists_pending_booking()
		{
			// Arrange
			var ev = await SeedEventAsync(TestData.Event(totalSeats: 10));
			var booking = Booking.CreatePending(ev.Id);

			// Act
			await using (var ctx = CreateContext())
			{
				await new BookingRepository(ctx).AddAsync(booking);
			}

			// Assert
			await using (var ctx = CreateContext())
			{
				var stored = await new BookingRepository(ctx).GetByIdAsync(booking.Id);
				stored.Should().NotBeNull();
				stored!.EventId.Should().Be(ev.Id);
				stored.Status.Should().Be(BookingStatus.Pending);
				stored.ProcessedAt.Should().BeNull();
			}
		}

		[Fact]
		public async Task AddAsync_saves_booking_and_seat_reservation_in_one_transaction()
		{
			// Arrange — событие с 5 местами
			var ev = await SeedEventAsync(TestData.Event(totalSeats: 5));

			// Act — резервируем место у отслеживаемого события и добавляем бронь
			// в рамках одного контекста (как это делает BookingService).
			await using (var ctx = CreateContext())
			{
				var eventRepository = new EventRepository(ctx);
				var bookingRepository = new BookingRepository(ctx);

				var tracked = await eventRepository.GetByIdAsync(ev.Id);
				tracked!.TryReserveSeats().Should().BeTrue();

				await bookingRepository.AddAsync(Booking.CreatePending(ev.Id));
			}

			// Assert — и бронь сохранена, и AvailableSeats уменьшилось
			await using (var ctx = CreateContext())
			{
				var stored = await new EventRepository(ctx).GetByIdAsync(ev.Id);
				stored!.AvailableSeats.Should().Be(4);
				(await ctx.Bookings.CountAsync()).Should().Be(1);
			}
		}

		[Fact]
		public async Task GetByIdAsync_returns_null_when_booking_missing()
		{
			// Arrange
			await using var ctx = CreateContext();

			// Act
			var result = await new BookingRepository(ctx).GetByIdAsync(Guid.NewGuid());

			// Assert
			result.Should().BeNull();
		}

		[Fact]
		public async Task UpdateAsync_persists_status_transition()
		{
			// Arrange
			var ev = await SeedEventAsync(TestData.Event(totalSeats: 3));
			var booking = Booking.CreatePending(ev.Id);
			await using (var ctx = CreateContext())
			{
				await new BookingRepository(ctx).AddAsync(booking);
			}

			// Act — переводим бронь в Confirmed
			var processedAt = DateTime.UtcNow;
			await using (var ctx = CreateContext())
			{
				var repository = new BookingRepository(ctx);
				var stored = await repository.GetByIdAsync(booking.Id);
				stored!.Confirm(processedAt);
				await repository.UpdateAsync(stored);
			}

			// Assert
			await using (var ctx = CreateContext())
			{
				var stored = await new BookingRepository(ctx).GetByIdAsync(booking.Id);
				stored!.Status.Should().Be(BookingStatus.Confirmed);
				stored.ProcessedAt.Should().NotBeNull();
			}
		}

		[Fact]
		public async Task GetPendingIdsAsync_returns_only_pending_bookings()
		{
			// Arrange
			var ev = await SeedEventAsync(TestData.Event(totalSeats: 10));
			var pending1 = Booking.CreatePending(ev.Id);
			var pending2 = Booking.CreatePending(ev.Id);
			var confirmed = Booking.CreatePending(ev.Id);
			confirmed.Confirm(DateTime.UtcNow);
			var rejected = Booking.CreatePending(ev.Id);
			rejected.Reject(DateTime.UtcNow);

			await using (var ctx = CreateContext())
			{
				ctx.Bookings.AddRange(pending1, pending2, confirmed, rejected);
				await ctx.SaveChangesAsync();
			}

			// Act
			await using var assertCtx = CreateContext();
			var ids = await new BookingRepository(assertCtx).GetPendingIdsAsync();

			// Assert
			ids.Should().BeEquivalentTo(new[] { pending1.Id, pending2.Id });
		}

		[Fact]
		public async Task AddAsync_violates_foreign_key_when_event_does_not_exist()
		{
			// Arrange — бронь ссылается на несуществующее событие
			var booking = Booking.CreatePending(Guid.NewGuid());

			// Act
			await using var ctx = CreateContext();
			var act = async () => await new BookingRepository(ctx).AddAsync(booking);

			// Assert — реальная БД отклоняет вставку из-за ограничения внешнего ключа
			await act.Should().ThrowAsync<DbUpdateException>();
		}

		private async Task<Event> SeedEventAsync(Event ev)
		{
			await using var ctx = CreateContext();
			ctx.Events.Add(ev);
			await ctx.SaveChangesAsync();
			return ev;
		}
	}
}
