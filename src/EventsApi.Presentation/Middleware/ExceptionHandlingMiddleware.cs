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

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

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

                NoAvailableSeatsException nase => (
                    nase.StatusCode,
                    "Конфликт",
                    nase.Message,
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

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app) =>
            app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
