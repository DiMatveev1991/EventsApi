using System.Text.Json.Serialization;
using EventsApi.BackgroundServices;
using EventsApi.DataAccess;
using EventsApi.Middleware;
using EventsApi.Repositories;
using EventsApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Слой данных: PostgreSQL через EF Core. DbContext регистрируется как scoped.
builder.Services.AddDbContext<AppDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Репозитории — scoped: делят scoped-контекст AppDbContext в пределах запроса.
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();

// Сервисы приложения — scoped, т. к. зависят от scoped-репозиториев.
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IBookingService, BookingService>();

// Фоновая обработка Pending-броней.
builder.Services.AddHostedService<BookingProcessor>();

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

	var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
	var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
	if (File.Exists(xmlPath))
		c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Применяем миграции EF Core при старте: схема БД (таблицы events, bookings и связи)
// создаётся и обновляется миграциями, а не EnsureCreated().
using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	db.Database.Migrate();
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