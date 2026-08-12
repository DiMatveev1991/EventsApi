namespace Users.Domain.Exceptions;

/// <summary>Базовое исключение ожидаемых ошибок Users.</summary>
public abstract class AppException(string message) : Exception(message)
{
    /// <summary>HTTP-код, соответствующий ошибке.</summary>
    public abstract int StatusCode { get; }
}

/// <summary>Ошибка нарушения правил входных данных или учётных данных.</summary>
public sealed class ValidationException : AppException
{
    /// <summary>Создаёт исключение валидации.</summary>
    public ValidationException(string message) : base(message) { }

    /// <inheritdoc />
    public override int StatusCode => 400;
}
