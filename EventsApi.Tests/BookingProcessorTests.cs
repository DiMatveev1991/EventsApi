using System.Reflection;
using Bookings.Application.Abstractions;
using Bookings.Application.BackgroundServices;
using Bookings.Domain.Entities;
using Bookings.Domain.Enums;
using Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventsApi.Tests;

public sealed class BookingProcessorTests
{
    [Fact]
    public async Task Processor_saves_confirmation_before_publishing_and_marks_delivery()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 2);
        var repository = new FakeBookingRepository(booking);
        var publisher = new FakePublisher(repository);
        using var provider = BuildProvider(repository, publisher);
        var processor = new BookingProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingProcessor>.Instance,
            Options.Create(new BookingProcessingOptions()));

        await InvokeProcessAsync(processor, booking.Id);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ConfirmationPublishedAt.Should().NotBeNull();
        repository.SaveCalls.Should().Be(2);
        publisher.WasConfirmedAndSavedAtPublish.Should().BeTrue();
        publisher.Messages.Should().ContainSingle(message =>
            message.BookingId == booking.Id &&
            message.EventId == booking.EventId &&
            message.UserId == booking.UserId &&
            message.Seats == booking.Seats);
    }

    [Fact]
    public async Task Publish_failure_leaves_confirmed_booking_for_retry()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1);
        var repository = new FakeBookingRepository(booking);
        var publisher = new FakePublisher(repository) { ThrowOnPublish = true };
        using var provider = BuildProvider(repository, publisher);
        var processor = new BookingProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingProcessor>.Instance,
            Options.Create(new BookingProcessingOptions()));

        await InvokeProcessAsync(processor, booking.Id);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ConfirmationPublishedAt.Should().BeNull();
        repository.SaveCalls.Should().Be(1);
        publisher.Messages.Should().ContainSingle();
    }

    private static ServiceProvider BuildProvider(
        FakeBookingRepository repository,
        FakePublisher publisher)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repository);
        services.AddSingleton(publisher);
        services.AddScoped<IBookingRepository>(serviceProvider =>
            serviceProvider.GetRequiredService<FakeBookingRepository>());
        services.AddScoped<IBookingEventPublisher>(serviceProvider =>
            serviceProvider.GetRequiredService<FakePublisher>());
        return services.BuildServiceProvider();
    }

    private static async Task InvokeProcessAsync(BookingProcessor processor, Guid bookingId)
    {
        var method = typeof(BookingProcessor).GetMethod(
            "ProcessAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var task = method!.Invoke(
            processor,
            new object[] { bookingId, CancellationToken.None }) as Task;
        task.Should().NotBeNull();
        await task!;
    }

    private sealed class FakeBookingRepository(Booking booking) : IBookingRepository
    {
        public int SaveCalls { get; private set; }

        public Task<Booking?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(id == booking.Id ? booking : null);

        public Task<IReadOnlyList<Guid>> GetPendingIdsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(new[] { booking.Id });

        public Task<IReadOnlyList<Guid>> GetUnpublishedConfirmedIdsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());

        public Task<int> CountActiveByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task AddAsync(
            Booking value,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePublisher(FakeBookingRepository repository)
        : IBookingEventPublisher
    {
        public List<BookingConfirmed> Messages { get; } = new();
        public bool ThrowOnPublish { get; init; }
        public bool WasConfirmedAndSavedAtPublish { get; private set; }

        public Task PublishAsync(
            BookingConfirmed message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            WasConfirmedAndSavedAtPublish = repository.SaveCalls >= 1;
            if (ThrowOnPublish)
                throw new InvalidOperationException("Kafka is unavailable");
            return Task.CompletedTask;
        }
    }
}
