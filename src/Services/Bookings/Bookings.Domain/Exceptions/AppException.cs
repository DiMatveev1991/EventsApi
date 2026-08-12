namespace Bookings.Domain.Exceptions;

public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => 404;
}

public sealed class ValidationException(string message) : AppException(message)
{
    public override int StatusCode => 400;
}

public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => 403;
}

public sealed class ActiveBookingLimitExceededException(int limit)
    : AppException($"Active booking limit exceeded: {limit}")
{
    public override int StatusCode => 409;
}
