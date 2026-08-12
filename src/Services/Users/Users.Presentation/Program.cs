using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using Users.Application.DependencyInjection;
using Users.Infrastructure.DependencyInjection;
using Users.Infrastructure.Persistence;
using Users.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Presentation выступает единственным composition root и связывает абстракции
// Application с реализациями Infrastructure.
builder.Services.AddUsersApplication();
builder.Services.AddUsersInfrastructure(builder.Configuration);
// MVC создаёт новый контроллер на каждый HTTP-запрос; scoped-сервис и DbContext
// живут в request scope и не разделяются между одновременными запросами.
builder.Services.AddControllers().AddJsonOptions(options =>
    // Роли сериализуются строками, чтобы HTTP-контракт не зависел от порядка enum.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Users выпускает JWT, но также настраивает полноценную проверку токена: это
// сохраняет корректную кнопку Authorize и готовит API к защищённым эндпоинтам.
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
// Проверяем минимальную длину HMAC-ключа до запуска приложения.
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
            // Небольшой clock skew допускает рассинхронизацию часов контейнеров.
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Users/Auth Service",
        Version = "v1",
        Description = "Registration, authentication and JWT issuance"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT issued by POST /auth/login"
    });
    // SecurityRequirement только настраивает отправку Bearer в Swagger;
    // доступ к операциям контролируется атрибутами авторизации.
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

// Миграция применяется до приёма запросов, чтобы регистрация не работала
// с устаревшей схемой базы данных.
using (var scope = app.Services.CreateScope())
{
    // UsersDbContext зарегистрирован как Scoped, поэтому миграция при старте
    // выполняется во временном scope и не удерживает контекст до остановки хоста.
    var database = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    await database.Database.MigrateAsync();
}

// Обработчик ошибок должен охватывать весь оставшийся HTTP-конвейер.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger оставлен включённым для воспроизводимой проверки учебного проекта.
app.UseSwagger();
app.UseSwaggerUI();

// Порядок обязателен: Authentication создаёт Principal, Authorization проверяет его.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health endpoint доступен без JWT для Docker и проверяет обязательный PostgreSQL.
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();

/// <summary>Маркер точки входа, используемый интеграционными тестами WebApplicationFactory.</summary>
public partial class Program { }
