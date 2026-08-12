using System.Security.Claims;
using System.Text;
using EventsApi.Application.DependencyInjection;
using EventsApi.Infrastructure.DependencyInjection;
using EventsApi.Infrastructure.Persistence;
using EventsApi.Presentation.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Presentation остаётся composition root: здесь связываются порты Application
// с реализациями Infrastructure, а сами нижние слои не зависят от Web API.
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
// MVC создаёт контроллер на каждый HTTP-запрос; его scoped-сервисы и DbContext
// принадлежат request scope и не используются одновременно разными запросами.
builder.Services.AddControllers();

// Все три API валидируют один и тот же issuer, audience и secret. Благодаря этому
// JWT, выданный Users, принимается Events и Bookings без синхронного вызова Users.
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
// Короткий HMAC-секрет делает подпись уязвимой, поэтому конфигурационная ошибка
// обнаруживается при старте, а не после выпуска небезопасных токенов.
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
            // Небольшой допуск компенсирует расхождение часов контейнеров, не
            // продлевая фактическое время жизни токена на стандартные пять минут.
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Events Service",
        Version = "v1",
        Description = "Event CRUD and eventually consistent seat availability"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    // Requirement добавляет кнопку Authorize и Bearer-заголовок в Swagger.
    // Фактические права по-прежнему задаются [Authorize] на эндпоинтах.
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

// Миграции выполняются до приёма трафика: API не начнёт обслуживать запросы
// со схемой БД, не соответствующей текущей модели.
using (var scope = app.Services.CreateScope())
{
    // DbContext зарегистрирован как Scoped, поэтому миграция выполняется
    // во временном startup-scope, который освобождается сразу после применения.
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await database.Database.MigrateAsync();
}

// Обработчик исключений ставится первым, чтобы сформировать Problem Details для
// ошибок всех следующих middleware и контроллеров.
app.UseGlobalExceptionHandler();

// Swagger включён для учебного проекта во всех окружениях, чтобы сервисы можно
// было проверить сразу после docker compose up.
app.UseSwagger();
app.UseSwaggerUI();

// Authentication должен заполнить HttpContext.User до проверки Authorization.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health endpoint открыт для Docker healthcheck. Redis намеренно не включён:
// кеш необязателен, и его недоступность не должна выводить Events из эксплуатации.
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();

/// <summary>Маркер точки входа, используемый интеграционными тестами WebApplicationFactory.</summary>
public partial class Program { }
