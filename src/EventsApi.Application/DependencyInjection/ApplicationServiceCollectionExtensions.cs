using EventsApi.Application.Abstractions;
using EventsApi.Application.BackgroundServices;
using EventsApi.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventsApi.Application.DependencyInjection
{
    /// <summary>
    /// Регистрация зависимостей слоя Application в DI-контейнере.
    /// Вызывается из composition root в Presentation.
    /// </summary>
    public static class ApplicationServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Один менеджер на процесс; внутри блокировки разделены по EventId.
            services.AddSingleton<IEventBookingLock, EventBookingLock>();

            // Сервисы приложения — scoped, т. к. зависят от scoped-репозиториев.
            services.AddScoped<IEventService, EventService>();
            services.AddScoped<IBookingService, BookingService>();

            // Фоновая обработка Pending-броней.
            services.AddHostedService<BookingProcessor>();

            return services;
        }
    }
}
