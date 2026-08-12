using Bookings.Application.Abstractions;
using Bookings.Infrastructure.Messaging;
using Bookings.Infrastructure.Persistence;
using Bookings.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookings.Infrastructure.DependencyInjection;

/// <summary>Содержит инфраструктурные регистрации Bookings.</summary>
public static class InfrastructureExtensions
{
    /// <summary>Регистрирует PostgreSQL, Kafka, health checks и репозиторий.</summary>
    public static IServiceCollection AddBookingsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // AddDbContext регистрирует Scoped: контекст принадлежит одному HTTP-
        // запросу или scope, явно созданному фоновым процессором.
        services.AddDbContext<BookingsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("BookingsDatabase")));
        services.AddHealthChecks()
            .AddDbContextCheck<BookingsDbContext>("database")
            .AddCheck<KafkaHealthCheck>("kafka");

        // Репозиторий Scoped, потому что использует scoped и непотокобезопасный DbContext.
        services.AddScoped<IBookingRepository, BookingRepository>();

        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");

        // Producer Confluent.Kafka тяжёлый и потокобезопасный; Singleton сохраняет
        // одно соединение и общий внутренний буфер на всё время жизни приложения.
        services.AddSingleton<IBookingEventPublisher>(
            _ => new KafkaBookingEventPublisher(bootstrapServers));

        return services;
    }
}
