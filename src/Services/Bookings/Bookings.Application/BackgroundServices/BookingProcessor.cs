using Bookings.Application.Abstractions;
using Bookings.Domain.Enums;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookings.Application.BackgroundServices;

/// <summary>
/// Confirms pending bookings and reliably retries unpublished confirmations.
/// The booking status is committed before the Kafka publish is attempted.
/// </summary>
public sealed class BookingProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<BookingProcessor> logger,
    IOptions<BookingProcessingOptions> options) : BackgroundService
{
    private readonly BookingProcessingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ids = await FindWorkAsync(stoppingToken);
                await Task.WhenAll(ids.Select(id => ProcessAsync(id, stoppingToken)));
                await Task.Delay(_options.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Booking processing cycle failed");
                await Task.Delay(_options.PollingInterval, stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<Guid>> FindWorkAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var pending = await repository.GetPendingIdsAsync(cancellationToken);
        var unpublished = await repository.GetUnpublishedConfirmedIdsAsync(cancellationToken);
        return pending.Concat(unpublished).Distinct().ToArray();
    }

    private async Task ProcessAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            if (_options.ConfirmationDelay > TimeSpan.Zero)
                await Task.Delay(_options.ConfirmationDelay, cancellationToken);
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var publisher = scope.ServiceProvider.GetRequiredService<IBookingEventPublisher>();
            var booking = await repository.GetByIdAsync(bookingId, cancellationToken);

            if (booking is null || booking.Status == BookingStatus.Cancelled)
                return;

            if (booking.Status == BookingStatus.Pending)
            {
                booking.Confirm(DateTimeOffset.UtcNow);
                await repository.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Booking {BookingId} persisted as Confirmed before publishing",
                    booking.Id);
            }

            if (booking.ConfirmationPublishedAt is not null)
                return;

            var message = new BookingConfirmed(
                booking.Id,
                booking.EventId,
                booking.UserId,
                booking.Seats,
                booking.ProcessedAt!.Value);

            await publisher.PublishAsync(message, cancellationToken);
            booking.MarkConfirmationPublished(DateTimeOffset.UtcNow);
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "BookingConfirmed for {BookingId} published and acknowledged locally",
                booking.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Booking {BookingId} was saved but its event was not acknowledged; it will be retried",
                bookingId);
        }
    }
}
