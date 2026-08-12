using Bookings.Application.Abstractions;
using Bookings.Infrastructure.Messaging;
using Bookings.Infrastructure.Persistence;
using Bookings.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookings.Infrastructure.DependencyInjection;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddBookingsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BookingsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("BookingsDatabase")));
        services.AddHealthChecks()
            .AddDbContextCheck<BookingsDbContext>("database")
            .AddCheck<KafkaHealthCheck>("kafka");
        services.AddScoped<IBookingRepository, BookingRepository>();

        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is required.");
        services.AddSingleton<IBookingEventPublisher>(
            _ => new KafkaBookingEventPublisher(bootstrapServers));

        return services;
    }
}
