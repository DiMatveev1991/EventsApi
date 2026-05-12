using EventsApi.DataAccess;
using EventsApi.DTOs;
using EventsApi.Exceptions;
using EventsApi.Services;
using FluentAssertions;
using Xunit;

namespace EventsApi.Tests;

public class BookingServiceTests
{
    private readonly IEventService _eventService;
    private readonly IBookingStore _bookingStore;
    private readonly BookingService _sut;

    public BookingServiceTests()
    {
        _eventService = new EventService();
        _bookingStore = new InMemoryBookingStore();
        _sut = new BookingService(_bookingStore, _eventService);
    }

    private EventDto CreateEvent() =>
        _eventService.Create(TestData.CreateDto());

    // ---------- Успешные сценарии ----------

    [Fact]
    public async Task CreateBookingAsync_ForExistingEvent_ReturnsPendingBooking()
    {
        // Arrange
        var ev = CreateEvent();

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
        var ev = CreateEvent();

        var dto = await _sut.CreateBookingAsync(ev.Id);

        _bookingStore.GetById(dto.Id).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_MultipleBookingsForSameEvent_AllHaveUniqueIds()
    {
        // Arrange
        var ev = CreateEvent();

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
        var ev = CreateEvent();
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
        var ev = CreateEvent();
        var created = await _sut.CreateBookingAsync(ev.Id);

        // Симулируем обработку: меняем статус через доменный метод и сохраняем.
        var entity = _bookingStore.GetById(created.Id)!;
        entity.Confirm(DateTime.UtcNow);
        _bookingStore.Update(entity);

        // Act
        var fetched = await _sut.GetBookingByIdAsync(created.Id);

        // Assert
        fetched.Status.Should().Be(BookingStatus.Confirmed);
        fetched.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReflectsStatusChange_AfterReject()
    {
        var ev = CreateEvent();
        var created = await _sut.CreateBookingAsync(ev.Id);

        var entity = _bookingStore.GetById(created.Id)!;
        entity.Reject(DateTime.UtcNow);
        _bookingStore.Update(entity);

        var fetched = await _sut.GetBookingByIdAsync(created.Id);

        fetched.Status.Should().Be(BookingStatus.Rejected);
        fetched.ProcessedAt.Should().NotBeNull();
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
        var ev = CreateEvent();
        _eventService.Delete(ev.Id);

        // Act
        var act = async () => await _sut.CreateBookingAsync(ev.Id);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
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
