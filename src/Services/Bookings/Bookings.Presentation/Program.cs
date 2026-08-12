using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Bookings.Application.DependencyInjection;
using Bookings.Application.BackgroundServices;
using Bookings.Infrastructure.DependencyInjection;
using Bookings.Infrastructure.Persistence;
using Bookings.Presentation.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Presentation является composition root и связывает Application с конкретной
// инфраструктурой, не разворачивая зависимости внутренних слоёв наружу.
builder.Services.AddBookingsApplication();

// Настройки фонового процессора валидируются на старте: отрицательная задержка
// или нулевой polling interval иначе вызвали бы цикл ошибок уже во время работы.
builder.Services.AddOptions<BookingProcessingOptions>()
    .Bind(builder.Configuration.GetSection(BookingProcessingOptions.SectionName))
    .Validate(options => options.ConfirmationDelay >= TimeSpan.Zero,
        "BookingProcessing:ConfirmationDelay cannot be negative.")
    .Validate(options => options.PollingInterval > TimeSpan.Zero,
        "BookingProcessing:PollingInterval must be positive.")
    .ValidateOnStart();
builder.Services.AddBookingsInfrastructure(builder.Configuration);
// MVC сам создаёт новый экземпляр контроллера на каждый HTTP-запрос. Его scoped-
// зависимости поэтому живут ровно в request scope и не разделяются между запросами.
builder.Services.AddControllers().AddJsonOptions(options =>
    // Строковые enum делают HTTP-контракт читаемым и независимым от числовых значений.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Bookings самостоятельно проверяет токен Users по общим JWT-настройкам и не
// создаёт синхронную зависимость от доступности Users API.
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
// HMAC secret короче 32 байт отклоняется при старте как небезопасная конфигурация.
if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("Jwt:Secret must contain at least 32 bytes.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            // 30 секунд компенсируют небольшое расхождение часов контейнеров.
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bookings Service",
        Version = "v1",
        Description = "Booking creation, cancellation and Kafka confirmation publishing"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    // Swagger получает Bearer-заголовок, а реальные ограничения задаёт [Authorize].
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = Array.Empty<string>()
    });
});

var app = builder.Build();

// Схема обновляется до запуска HTTP-конвейера и фонового процессора.
using (var scope = app.Services.CreateScope())
{
    // DbContext — Scoped, поэтому для миграции при старте создаётся временный scope.
    var database = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
    await database.Database.MigrateAsync();
}

// Middleware ошибок расположен первым и преобразует исключения контроллеров
// и следующих компонентов конвейера в единый Problem Details.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger доступен в учебном окружении для ручного E2E-сценария.
app.UseSwagger();
app.UseSwaggerUI();

// Сначала строится User из JWT, затем проверяются роли и политики доступа.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Анонимный health endpoint нужен оркестратору и проверяет обязательные зависимости:
// PostgreSQL и Kafka. Без них Bookings не может выполнить основной сценарий.
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();

/// <summary>Маркер точки входа, используемый интеграционными тестами WebApplicationFactory.</summary>
public partial class Program { }
