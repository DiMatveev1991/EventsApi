namespace EventsApi.Domain.Exceptions
{
    /// <summary>
    /// Базовое доменное исключение для прикладной логики. Позволяет верхним слоям
    /// (например, middleware в Presentation) различать ожидаемые ошибки
    /// (валидация, отсутствие ресурса) и непредвиденные сбои.
    /// </summary>
    /// <remarks>
    /// <see cref="StatusCode"/> хранит подходящий HTTP-код числом, чтобы Domain
    /// оставался независимым от ASP.NET Core: сопоставление с реальным ответом
    /// выполняет слой Presentation.
    /// </remarks>
    public abstract class AppException : Exception
    {
        public abstract int StatusCode { get; }

        protected AppException(string message) : base(message) { }
        protected AppException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>404 Not Found — запрошенный ресурс не найден.</summary>
    public sealed class NotFoundException : AppException
    {
        public override int StatusCode => 404;

        public NotFoundException(string message) : base(message) { }

        public static NotFoundException ForEvent(Guid id) =>
            new($"Мероприятие с ID {id} не найдено");

    }

    /// <summary>400 Bad Request — нарушены правила валидации.</summary>
    public sealed class ValidationException : AppException
    {
        public override int StatusCode => 400;

        public IReadOnlyDictionary<string, string[]>? Errors { get; }

        public ValidationException(string message) : base(message) { }

        public ValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
            : base(message)
        {
            Errors = errors;
        }
    }

}
