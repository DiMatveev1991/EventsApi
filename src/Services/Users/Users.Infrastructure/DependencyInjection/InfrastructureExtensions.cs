using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Users.Application.Abstractions;
using Users.Infrastructure.Persistence;
using Users.Infrastructure.Repositories;
using Users.Infrastructure.Security;

namespace Users.Infrastructure.DependencyInjection;

/// <summary>Содержит инфраструктурные регистрации Users.</summary>
public static class InfrastructureExtensions
{
    /// <summary>Регистрирует PostgreSQL, безопасность, health checks и репозиторий.</summary>
    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // AddDbContext использует Scoped: один непотокобезопасный контекст
        // принадлежит одному HTTP-запросу.
        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("UsersDatabase")));
        services.AddHealthChecks().AddDbContextCheck<UsersDbContext>("database");

        // Репозиторий Scoped, потому что зависит от scoped UsersDbContext.
        services.AddScoped<IUserRepository, UserRepository>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // Оба сервиса не хранят состояние запроса и потокобезопасны, поэтому
        // Singleton исключает лишние экземпляры без смешивания пользовательских данных.
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        return services;
    }
}
