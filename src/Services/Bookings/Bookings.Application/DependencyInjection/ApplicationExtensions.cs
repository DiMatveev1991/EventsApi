using Bookings.Application.BackgroundServices;
using Bookings.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bookings.Application.DependencyInjection;

public static class ApplicationExtensions
{
    public static IServiceCollection AddBookingsApplication(this IServiceCollection services)
    {
        services.AddScoped<IBookingService, BookingService>();
        services.AddHostedService<BookingProcessor>();
        return services;
    }
}
