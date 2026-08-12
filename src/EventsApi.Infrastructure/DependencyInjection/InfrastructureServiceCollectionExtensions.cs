using EventsApi.Application.Abstractions;
using EventsApi.Application.Caching;
using EventsApi.Infrastructure.Caching;
using EventsApi.Infrastructure.Messaging;
using EventsApi.Infrastructure.Persistence;
using EventsApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

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
                options.UseNpgsql(configuration.GetConnectionString("EventsDatabase")));

            // Реализации портов — scoped: делят scoped-контекст AppDbContext в пределах запроса.
            services.AddScoped<IEventRepository, EventRepository>();

            services.Configure<CacheOptions>(
                configuration.GetSection(CacheOptions.SectionName));
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var connectionString = configuration["Redis:ConnectionString"]
                    ?? "localhost:6379";
                var options = ConfigurationOptions.Parse(connectionString);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 1;
                options.ConnectTimeout = 1_000;
                options.SyncTimeout = 1_000;
                return ConnectionMultiplexer.Connect(options);
            });
            services.AddSingleton<ICacheService, RedisCacheService>();

            services.AddHostedService<KafkaTopicInitializer>();
            services.AddHostedService<BookingConfirmedConsumer>();

            return services;
        }
    }
}
