namespace EventsApi.Application.Messaging;

public enum BookingConfirmationResult
{
    Applied,
    Duplicate,
    EventNotFound,
    InsufficientSeats
}
