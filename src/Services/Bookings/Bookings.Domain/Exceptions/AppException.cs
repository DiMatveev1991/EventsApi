namespace Bookings.Domain.Exceptions;

/// <summary>Базовое исключение ожидаемых ошибок Bookings.</summary>
public abstract class AppException(string message) : Exception(message)
{
    /// <summary>HTTP-код, соответствующий ошибке.</summary>
    public abstract int StatusCode { get; }
}

/// <summary>Ошибка отсутствующего бронирования.</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
    /// <inheritdoc />
    public override int StatusCode => 404;
}

/// <summary>Ошибка нарушения правил входных данных или состояния.</summary>
public sealed class ValidationException(string message) : AppException(message)
{
    /// <inheritdoc />
    public override int StatusCode => 400;
}

/// <summary>Ошибка доступа к чужому бронированию.</summary>
public sealed class ForbiddenException(string message) : AppException(message)
{
    /// <inheritdoc />
    public override int StatusCode => 403;
}

/// <summary>Ошибка превышения лимита активных бронирований.</summary>
public sealed class ActiveBookingLimitExceededException(int limit)
    : AppException($"Active booking limit exceeded: {limit}")
{
    /// <inheritdoc />
    public override int StatusCode => 409;
}
