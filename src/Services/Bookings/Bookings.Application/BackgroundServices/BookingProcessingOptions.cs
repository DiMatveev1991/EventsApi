namespace Bookings.Application.BackgroundServices;

/// <summary>Настройки фоновой обработки бронирований.</summary>
public sealed class BookingProcessingOptions
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "BookingProcessing";

    /// <summary>Опциональная задержка перед подтверждением бронирования.</summary>
    public TimeSpan ConfirmationDelay { get; init; } = TimeSpan.Zero;

    /// <summary>Пауза между циклами поиска необработанных бронирований.</summary>
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromMilliseconds(500);
}
