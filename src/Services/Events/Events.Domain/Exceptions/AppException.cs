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
        /// <summary>HTTP-код, соответствующий ожидаемой ошибке приложения.</summary>
        public abstract int StatusCode { get; }

        /// <summary>Создаёт исключение с пользовательским сообщением.</summary>
        protected AppException(string message) : base(message) { }

        /// <summary>Создаёт исключение с сообщением и исходной причиной.</summary>
        protected AppException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>404 Not Found — запрошенный ресурс не найден.</summary>
    public sealed class NotFoundException : AppException
    {
        public override int StatusCode => 404;

        /// <summary>Создаёт исключение отсутствующего ресурса.</summary>
        public NotFoundException(string message) : base(message) { }

        /// <summary>Создаёт исключение для отсутствующего события.</summary>
        public static NotFoundException ForEvent(Guid id) =>
            new($"Мероприятие с ID {id} не найдено");

    }

    /// <summary>400 Bad Request — нарушены правила валидации.</summary>
    public sealed class ValidationException : AppException
    {
        public override int StatusCode => 400;

        /// <summary>Ошибки, сгруппированные по именам полей.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; }

        /// <summary>Создаёт исключение валидации без детализации по полям.</summary>
        public ValidationException(string message) : base(message) { }

        /// <summary>Создаёт исключение валидации с ошибками отдельных полей.</summary>
        public ValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
            : base(message)
        {
            Errors = errors;
        }
    }

}
