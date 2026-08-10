using System.Text.Json.Serialization;
using EventsApi.Application.DependencyInjection;
using EventsApi.Infrastructure.DependencyInjection;
using EventsApi.Infrastructure.Persistence;
using EventsApi.Presentation.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ─── Composition root ──────────────────────────────────────────────────────
// Регистрация зависимостей слоёв через extension-методы, чтобы Program.cs
// оставался компактным. Application не знает об Infrastructure — их связывает
// DI-контейнер здесь, в Presentation.
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
// ───────────────────────────────────────────────────────────────────────────

builder.Services
    .AddControllers()
    .AddJsonOptions(opts =>
    {
        // BookingStatus и другие enum-ы сериализуем строкой ("Pending"/"Confirmed"/"Rejected"),
        // а не числом — читаемее и в Swagger, и в ответах API.
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

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

    // Чтобы Swagger показывал enum-ы строками, согласованно с сериализатором.
    c.UseInlineDefinitionsForEnums();

    // Подтягиваем XML-комментарии контроллеров (Presentation) и DTO (Application),
    // чтобы описания эндпоинтов и моделей отображались в Swagger.
    foreach (var xmlFile in new[] { "EventsApi.Presentation.xml", "EventsApi.Application.xml" })
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Применяем миграции EF Core при старте: схема БД (таблицы events, bookings и связи)
// создаётся и обновляется миграциями, а не EnsureCreated().
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateWithLegacyBaselineAsync();
}

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
