using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Models;
using EventsApi.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.Tests;

public class BookingServiceTests : IDisposable
{
	private readonly ServiceProvider _sp;
	private readonly IBookingService _sut;
	private readonly IEventService _eventService;
	private readonly AppDbContext _context;

	public BookingServiceTests()
	{
		_sp = TestHost.Build();
		_sut = _sp.GetRequiredService<IBookingService>();
		_eventService = _sp.GetRequiredService<IEventService>();
		_context = _sp.GetRequiredService<AppDbContext>();
	}

	public void Dispose() => _sp.Dispose();

	private Task<EventDto> CreateTestEvent(int totalSeats = 10) =>
		_eventService.CreateAsync(TestData.CreateDto(totalSeats: totalSeats));

	// ---------- Успешные сценарии ----------

	[Fact]
	public async Task CreateBookingAsync_ForExistingEvent_ReturnsPendingBooking()
	{
		// Arrange
		var ev = await CreateTestEvent();

		// Act
		var booking = await _sut.CreateBookingAsync(ev.Id);

		// Assert
		booking.Should().NotBeNull();
		booking.Id.Should().NotBe(Guid.Empty);
		booking.EventId.Should().Be(ev.Id);
		booking.Status.Should().Be(BookingStatus.Pending);
		booking.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
		booking.ProcessedAt.Should().BeNull();
	}

	[Fact]
	public async Task CreateBookingAsync_PersistsBookingInStore()
	{
		var ev = await CreateTestEvent();

		var dto = await _sut.CreateBookingAsync(ev.Id);

		(await _context.Bookings.AsNoTracking()
			.FirstOrDefaultAsync(b => b.Id == dto.Id)).Should().NotBeNull();
	}

	[Fact]
	public async Task CreateBookingAsync_DecreasesAvailableSeats_AfterEachBooking()
	{
		// Arrange
		var ev = await CreateTestEvent(totalSeats: 3);

		// Act + Assert: после каждой успешной брони AvailableSeats уменьшается на 1.
		for (var i = 1; i <= 3; i++)
		{
			await _sut.CreateBookingAsync(ev.Id);
			(await _eventService.GetByIdAsync(ev.Id)).AvailableSeats.Should().Be(3 - i);
		}
	}

	[Fact]
	public async Task CreateBookingAsync_UpToLimit_AllSucceedWithUniqueIds()
	{
		// Arrange
		var ev = await CreateTestEvent(totalSeats: 5);

		// Act: создаём брони до лимита.
		var bookings = new List<BookingDto>();
		for (var i = 0; i < 5; i++)
			bookings.Add(await _sut.CreateBookingAsync(ev.Id));

		// Assert
		bookings.Should().HaveCount(5);
		bookings.Select(b => b.Id).Should().OnlyHaveUniqueItems();
		bookings.Should().OnlyContain(b => b.Status == BookingStatus.Pending);
		(await _eventService.GetByIdAsync(ev.Id)).AvailableSeats.Should().Be(0);
	}

	[Fact]
	public async Task CreateBookingAsync_MultipleBookingsForSameEvent_AllHaveUniqueIds()
	{
		// Arrange
		var ev = await CreateTestEvent();

		// Act
		var a = await _sut.CreateBookingAsync(ev.Id);
		var b = await _sut.CreateBookingAsync(ev.Id);
		var c = await _sut.CreateBookingAsync(ev.Id);

		// Assert
		var ids = new[] { a.Id, b.Id, c.Id };
		ids.Should().OnlyHaveUniqueItems();
		new[] { a, b, c }.Should().OnlyContain(x => x.EventId == ev.Id);
		new[] { a, b, c }.Should().OnlyContain(x => x.Status == BookingStatus.Pending);
	}

	[Fact]
	public async Task GetBookingByIdAsync_ExistingId_ReturnsCorrectBooking()
	{
		var ev = await CreateTestEvent();
		var created = await _sut.CreateBookingAsync(ev.Id);

		var fetched = await _sut.GetBookingByIdAsync(created.Id);

		fetched.Id.Should().Be(created.Id);
		fetched.EventId.Should().Be(ev.Id);
		fetched.Status.Should().Be(BookingStatus.Pending);
	}

	[Fact]
	public async Task GetBookingByIdAsync_ReflectsStatusChange_AfterConfirm()
	{
		// Arrange
		var ev = await CreateTestEvent();
		var created = await _sut.CreateBookingAsync(ev.Id);

		// Симулируем обработку: меняем статус через доменный метод и сохраняем.
		var entity = await _context.Bookings.FirstAsync(b => b.Id == created.Id);
		entity.Confirm(DateTime.UtcNow);
		await _context.SaveChangesAsync();

		// Act
		var fetched = await _sut.GetBookingByIdAsync(created.Id);

		// Assert
		fetched.Status.Should().Be(BookingStatus.Confirmed);
		fetched.ProcessedAt.Should().NotBeNull();
	}

	[Fact]
	public async Task GetBookingByIdAsync_ReflectsStatusChange_AfterReject()
	{
		var ev = await CreateTestEvent();
		var created = await _sut.CreateBookingAsync(ev.Id);

		var entity = await _context.Bookings.FirstAsync(b => b.Id == created.Id);
		entity.Reject(DateTime.UtcNow);
		await _context.SaveChangesAsync();

		var fetched = await _sut.GetBookingByIdAsync(created.Id);

		fetched.Status.Should().Be(BookingStatus.Rejected);
		fetched.ProcessedAt.Should().NotBeNull();
	}

	// ---------- Reject + ReleaseSeats ----------

	[Fact]
	public async Task RejectAndReleaseSeats_RestoresAvailableSeats()
	{
		// Arrange: единственное место занято.
		var ev = await CreateTestEvent(totalSeats: 1);
		var created = await _sut.CreateBookingAsync(ev.Id);
		(await _eventService.GetByIdAsync(ev.Id)).AvailableSeats.Should().Be(0);

		// Act: отклоняем бронь и возвращаем место в пул.
		var booking = await _context.Bookings.FirstAsync(b => b.Id == created.Id);
		booking.Reject(DateTime.UtcNow);
		var entity = await _context.Events.FirstAsync(e => e.Id == ev.Id);
		entity.ReleaseSeats();
		await _context.SaveChangesAsync();

		// Assert
		(await _eventService.GetByIdAsync(ev.Id)).AvailableSeats.Should().Be(1);
	}

	[Fact]
	public async Task RejectAndReleaseSeats_AllowsNewBookingForSameSeat()
	{
		// Arrange: единственное место занято.
		var ev = await CreateTestEvent(totalSeats: 1);
		var first = await _sut.CreateBookingAsync(ev.Id);

		var booking = await _context.Bookings.FirstAsync(b => b.Id == first.Id);
		booking.Reject(DateTime.UtcNow);
		var entity = await _context.Events.FirstAsync(e => e.Id == ev.Id);
		entity.ReleaseSeats();
		await _context.SaveChangesAsync();

		// Act: на освободившееся место можно создать новую бронь.
		var second = await _sut.CreateBookingAsync(ev.Id);

		// Assert
		second.Id.Should().NotBe(first.Id);
		second.Status.Should().Be(BookingStatus.Pending);
		(await _eventService.GetByIdAsync(ev.Id)).AvailableSeats.Should().Be(0);
	}

	// ---------- Неуспешные сценарии ----------

	[Fact]
	public async Task CreateBookingAsync_ForNonExistentEvent_ThrowsNotFound()
	{
		var unknownEventId = Guid.NewGuid();

		var act = async () => await _sut.CreateBookingAsync(unknownEventId);

		await act.Should()
			.ThrowAsync<NotFoundException>()
			.Where(ex => ex.StatusCode == 404)
			.Where(ex => ex.Message.Contains(unknownEventId.ToString()));
	}

	[Fact]
	public async Task CreateBookingAsync_ForDeletedEvent_ThrowsNotFound()
	{
		// Arrange
		var ev = await CreateTestEvent();
		await _eventService.DeleteAsync(ev.Id);

		// Act
		var act = async () => await _sut.CreateBookingAsync(ev.Id);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>();
	}

	[Fact]
	public async Task CreateBookingAsync_WhenSeatsExhausted_ThrowsNoAvailableSeats()
	{
		// Arrange: исчерпываем единственное место.
		var ev = await CreateTestEvent(totalSeats: 1);
		await _sut.CreateBookingAsync(ev.Id);

		// Act: следующая попытка должна упасть с 409.
		var act = async () => await _sut.CreateBookingAsync(ev.Id);

		// Assert
		await act.Should()
			.ThrowAsync<NoAvailableSeatsException>()
			.Where(ex => ex.StatusCode == 409)
			.WithMessage("No available seats for this event");

		// Лишняя бронь не создана, мест не появилось.
		(await _context.Bookings.CountAsync()).Should().Be(1);
		(await _eventService.GetByIdAsync(ev.Id)).AvailableSeats.Should().Be(0);
	}

	[Fact]
	public async Task GetBookingByIdAsync_NonExistentId_ThrowsNotFound()
	{
		var unknownBookingId = Guid.NewGuid();

		var act = async () => await _sut.GetBookingByIdAsync(unknownBookingId);

		await act.Should()
			.ThrowAsync<NotFoundException>()
			.Where(ex => ex.Message.Contains(unknownBookingId.ToString()));
	}
}