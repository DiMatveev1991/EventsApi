using Bookings.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.Presentation.Middleware;

/// <summary>Преобразует исключения Bookings в безопасные ответы Problem Details.</summary>
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
            var status = exception switch
            {
                AppException appException => appException.StatusCode,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            };

            if (status >= 500)
                logger.LogError(exception, "Unhandled Bookings service error");
            else
                logger.LogWarning(exception, "Bookings request rejected");

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
