using System.Text.Json;
using EventsApi.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace EventsApi.Presentation.Middleware
{
    /// <summary>
    /// Перехватывает все необработанные исключения и возвращает
    /// единообразный JSON-ответ в формате RFC 7807 (Problem Details).
    /// Сопоставляет доменные исключения с HTTP-статусами.
    /// </summary>
    public sealed class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>Создаёт middleware с доступом к следующему обработчику и окружению.</summary>
        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        /// <summary>Передаёт запрос дальше и перехватывает необработанные исключения.</summary>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        /// <summary>Преобразует исключение в единообразный ответ Problem Details.</summary>
        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, title, detail, errors) = MapException(exception);

            if (statusCode >= 500)
                _logger.LogError(exception, "Необработанное исключение: {Message}", exception.Message);
            else
                _logger.LogWarning(exception, "Обработанное исключение ({Status}): {Message}",
                    statusCode, exception.Message);

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            };

            if (errors is not null)
                problem.Extensions["errors"] = errors;

            if (_env.IsDevelopment() && statusCode >= 500)
                problem.Extensions["trace"] = exception.ToString();

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            var payload = JsonSerializer.Serialize(problem, JsonOptions);
            await context.Response.WriteAsync(payload);
        }

        /// <summary>Сопоставляет исключение со статусом и безопасным текстом ответа.</summary>
        private static (int statusCode, string title, string detail, IReadOnlyDictionary<string, string[]>? errors)
            MapException(Exception ex)
        {
            return ex switch
            {
                ValidationException ve => (
                    ve.StatusCode,
                    "Ошибка валидации",
                    ve.Message,
                    ve.Errors),

                NotFoundException nfe => (
                    nfe.StatusCode,
                    "Ресурс не найден",
                    nfe.Message,
                    null),

                AppException ae => (
                    ae.StatusCode,
                    "Ошибка приложения",
                    ae.Message,
                    null),

                _ => (
                    StatusCodes.Status500InternalServerError,
                    "Внутренняя ошибка сервера",
                    "Произошла непредвиденная ошибка. Повторите попытку позже.",
                    null)
            };
        }
    }

    /// <summary>Содержит расширение для подключения глобального обработчика ошибок.</summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        /// <summary>Добавляет глобальный ExceptionHandlingMiddleware в HTTP-конвейер.</summary>
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app) =>
            app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
