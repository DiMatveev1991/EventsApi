namespace Users.Domain.Exceptions;

public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

public sealed class ValidationException : AppException
{
    public ValidationException(string message) : base(message) { }

    public override int StatusCode => 400;
}
