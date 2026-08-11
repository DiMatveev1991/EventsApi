using EventsApi.Application.BackgroundServices;
using EventsApi.Application.Services;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Enums;
using EventsApi.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventsApi.Tests;

public class BookingProcessorTests : IDisposable
{
    /// <summary>
    /// Время ожидания обработки. Сервис делает Task.Delay(2 сек) на каждую бронь
    /// и опрашивает раз в 500 мс. 10 секунд — с большим запасом для CI.
    /// </summary>
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(10);

    private readonly ServiceProvider _sp;

    public BookingProcessorTests()
    {
        _sp = TestHost.Build();
    }

    public void Dispose() => _sp.Dispose();

    private BookingProcessor CreateProcessor() =>
        new(_sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingProcessor>.Instance);

    private async Task<Guid> CreateEventAsync(int totalSeats = 100)
    {
        using var scope = _sp.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<IEventService>();
        var ev = await events.CreateAsync(TestData.CreateDto(totalSeats: totalSeats));
        return ev.Id;
    }

    private async Task<Guid> AddPendingBookingAsync(Guid eventId)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = Booking.CreatePending(eventId, Guid.NewGuid());
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking.Id;
    }

    private async Task DeleteEventAsync(Guid eventId)
    {
        // Отдельный scope: событие удаляется без отслеживания брони, поэтому бронь
        // остаётся в базе и будет отклонена фоновым сервисом.
        using var scope = _sp.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<IEventService>();
        await events.DeleteAsync(eventId);
    }

    private async Task<Booking?> GetBookingAsync(Guid bookingId)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookingId);
    }

    private async Task<int> PendingCountAsync()
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending);
    }

    private async Task<List<Booking>> AllBookingsAsync()
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings.AsNoTracking().ToListAsync();
    }

    private async Task<Booking> WaitUntilProcessedAsync(Guid bookingId)
    {
        var deadline = DateTime.UtcNow + MaxWait;
        while (DateTime.UtcNow < deadline)
        {
            var booking = await GetBookingAsync(bookingId);
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
        var evId = await CreateEventAsync();
        var bookingId = await AddPendingBookingAsync(evId);
        var processor = CreateProcessor();

        // Act
        await processor.StartAsync(CancellationToken.None);
        var processed = await WaitUntilProcessedAsync(bookingId);
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
        var evId = await CreateEventAsync();
        var bookingId = await AddPendingBookingAsync(evId);

        // Удаляем событие до того, как фоновый сервис до него доберётся.
        await DeleteEventAsync(evId);

        // Act
        var processor = CreateProcessor();
        await processor.StartAsync(CancellationToken.None);
        var processed = await WaitUntilProcessedAsync(bookingId);
        await processor.StopAsync(CancellationToken.None);

        // Assert
        processed.Status.Should().Be(BookingStatus.Rejected);
        processed.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessesMultiplePendingBookings_InParallel()
    {
        // Arrange
        var evId = await CreateEventAsync();
        await AddPendingBookingAsync(evId);
        await AddPendingBookingAsync(evId);
        await AddPendingBookingAsync(evId);

        // Act
        var started = DateTime.UtcNow;
        var processor = CreateProcessor();
        await processor.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (await PendingCountAsync() == 0) break;
            await Task.Delay(200);
        }
        var elapsed = DateTime.UtcNow - started;

        await processor.StopAsync(CancellationToken.None);

        // Assert
        (await PendingCountAsync()).Should().Be(0);
        (await AllBookingsAsync()).Should().OnlyContain(b => b.Status == BookingStatus.Confirmed);

        // Задержки выполняются параллельно: 3 брони по 2 сек обрабатываются
        // значительно быстрее, чем 6 сек последовательной обработки.
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(6));
    }

    [Fact]
    public async Task StopAsync_CancelsGracefully_WithoutThrowing()
    {
        // Arrange
        var processor = CreateProcessor();

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
        var processor = CreateProcessor();

        // Act
        await processor.StartAsync(CancellationToken.None);
        await Task.Delay(1500); // даём сервису поработать на пустой базе
        await processor.StopAsync(CancellationToken.None);

        // Assert
        (await AllBookingsAsync()).Should().BeEmpty();
    }
}
