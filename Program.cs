using EventsApi.BackgroundServices;
using EventsApi.DataAccess;
using EventsApi.Middleware;
using EventsApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Сервисы приложения
builder.Services.AddSingleton<IEventService, EventService>();

// Бронирования: хранилище + сервис (Singleton, т. к. данные in-memory).
builder.Services.AddSingleton<IBookingStore, InMemoryBookingStore>();
builder.Services.AddSingleton<IBookingService, BookingService>();

// Фоновая обработка Pending-броней.
builder.Services.AddHostedService<BookingProcessor>();

builder.Services.AddControllers();

// Возвращаем ModelState-ошибки валидации в том же формате ProblemDetails,
// что и наш middleware — единообразный ответ при 400.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value is { Errors.Count: > 0 })
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Ошибка валидации",
            Detail = "Один или несколько параметров запроса некорректны",
            Instance = context.HttpContext.Request.Path
        };

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EventsApi",
        Version = "v1",
        Description = "REST API для управления мероприятиями"
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Middleware должен стоять раньше всех остальных, чтобы ловить любые исключения.
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

// Нужен для WebApplicationFactory в интеграционных тестах (не обязательно в этом спринте).
public partial class Program { }
