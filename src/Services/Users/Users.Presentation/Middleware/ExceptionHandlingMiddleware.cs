using Microsoft.AspNetCore.Mvc;
using Users.Domain.Exceptions;

namespace Users.Presentation.Middleware;

/// <summary>Преобразует исключения Users в безопасные ответы Problem Details.</summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>Передаёт запрос дальше и обрабатывает необработанные исключения.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var status = exception is AppException appException
                ? appException.StatusCode
                : StatusCodes.Status500InternalServerError;

            if (status >= 500)
                logger.LogError(exception, "Unhandled Users service error");
            else
                logger.LogWarning(exception, "Users request rejected");

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = status >= 500 ? "Internal Server Error" : "Request failed",
                Detail = status >= 500 ? "An unexpected error occurred." : exception.Message,
                Instance = context.Request.Path
            });
        }
    }
}
