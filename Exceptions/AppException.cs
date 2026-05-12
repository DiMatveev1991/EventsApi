namespace EventsApi.Exceptions
{
    /// <summary>
    /// Базовое исключение для прикладной логики. Позволяет middleware
    /// различать ожидаемые ошибки (валидация, отсутствие ресурса)
    /// и непредвиденные сбои.
    /// </summary>
    public abstract class AppException : Exception
    {
        public abstract int StatusCode { get; }

        protected AppException(string message) : base(message) { }
        protected AppException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>404 Not Found.</summary>
    public sealed class NotFoundException : AppException
    {
        public override int StatusCode => StatusCodes.Status404NotFound;

        public NotFoundException(string message) : base(message) { }

        public static NotFoundException ForEvent(Guid id) =>
            new($"Мероприятие с ID {id} не найдено");

        public static NotFoundException ForBooking(Guid id) =>
            new($"Бронь с ID {id} не найдена");
    }

    /// <summary>400 Bad Request.</summary>
    public sealed class ValidationException : AppException
    {
        public override int StatusCode => StatusCodes.Status400BadRequest;

        public IReadOnlyDictionary<string, string[]>? Errors { get; }

        public ValidationException(string message) : base(message) { }

        public ValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
            : base(message)
        {
            Errors = errors;
        }
    }
}
