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
        /// <summary>Регистрирует PostgreSQL, Redis, Kafka и реализации портов Events.</summary>
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services, IConfiguration configuration)
        {
            // AddDbContext использует Scoped: один DbContext обслуживает один HTTP-запрос
            // или явно созданный scope Kafka-обработчика и не разделяется между потоками.
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("EventsDatabase")));
            services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>("database")
                .AddCheck<KafkaHealthCheck>("kafka");

            // Репозиторий Scoped, потому что он хранит ссылку на scoped AppDbContext.
            services.AddScoped<IEventRepository, EventRepository>();

            services.Configure<CacheOptions>(
                configuration.GetSection(CacheOptions.SectionName));

            // ConnectionMultiplexer — тяжёлый потокобезопасный клиент. Singleton
            // переиспользует соединения и предотвращает socket exhaustion.
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var connectionString = configuration["Redis:ConnectionString"]
                    ?? "localhost:6379";
                var options = ConfigurationOptions.Parse(connectionString);
                // Не прерываем запуск Events без Redis: Cache-Aside продолжит
                // обслуживать запросы непосредственно из PostgreSQL.
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 1;
                // Короткие таймауты ограничивают задержку деградировавшего кеша.
                options.ConnectTimeout = 1_000;
                options.SyncTimeout = 1_000;
                return ConnectionMultiplexer.Connect(options);
            });

            // RedisCacheService не хранит request-state и безопасно разделяет
            // singleton-подключение между всеми запросами.
            services.AddSingleton<ICacheService, RedisCacheService>();

            // Hosted services по контракту хоста являются Singleton. Consumer сам
            // создаёт scope для каждого сообщения перед получением scoped-зависимостей.
            services.AddHostedService<KafkaTopicInitializer>();
            services.AddHostedService<BookingConfirmedConsumer>();

            return services;
        }
    }
}
