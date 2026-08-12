using EventsApi.Application.Messaging;
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
        /// <summary>Регистрирует прикладные сценарии Events в DI-контейнере.</summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Сервисы scoped, потому что каждый HTTP-запрос или Kafka-сообщение
            // должно работать со своим scoped-репозиторием и DbContext.
            services.AddScoped<IEventService, EventService>();
            services.AddScoped<IBookingConfirmedHandler, BookingConfirmedHandler>();

            return services;
        }
    }
}
