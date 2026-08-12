namespace Bookings.Application.BackgroundServices;

public sealed class BookingProcessingOptions
{
    public const string SectionName = "BookingProcessing";

    public TimeSpan ConfirmationDelay { get; init; } = TimeSpan.Zero;
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromMilliseconds(500);
}
