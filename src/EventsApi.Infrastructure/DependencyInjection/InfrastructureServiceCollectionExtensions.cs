using EventsApi.Application.Abstractions;
using EventsApi.Infrastructure.Persistence;
using EventsApi.Infrastructure.Repositories;
using EventsApi.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventsApi.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Регистрация инфраструктурных зависимостей (БД, репозитории) в DI-контейнере.
    /// Вызывается из composition root в Presentation.
    /// </summary>
    public static class InfrastructureServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services, IConfiguration configuration)
        {
            // Слой данных: PostgreSQL через EF Core. DbContext регистрируется как scoped.
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            // Реализации портов — scoped: делят scoped-контекст AppDbContext в пределах запроса.
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<IUserRepository, UserRepository>();

            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            services.AddSingleton<IPasswordHasher, Sha256PasswordHasher>();
            services.AddSingleton<ITokenService, JwtTokenService>();

            return services;
        }
    }
}
