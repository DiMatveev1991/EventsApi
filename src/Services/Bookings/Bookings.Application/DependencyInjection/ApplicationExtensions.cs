using Bookings.Application.BackgroundServices;
using Bookings.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bookings.Application.DependencyInjection;

/// <summary>Содержит регистрации прикладного слоя Bookings.</summary>
public static class ApplicationExtensions
{
    /// <summary>Регистрирует сервис бронирований и фоновый процессор.</summary>
    public static IServiceCollection AddBookingsApplication(this IServiceCollection services)
    {
        // BookingService Scoped, потому что зависит от scoped-репозитория и должен
        // использовать тот же DbContext в пределах одного HTTP-запроса.
        services.AddScoped<IBookingService, BookingService>();

        // AddHostedService регистрирует Singleton на время жизни хоста. Scoped-
        // зависимости процессор разрешает сам через IServiceScopeFactory.
        services.AddHostedService<BookingProcessor>();
        return services;
    }
}
