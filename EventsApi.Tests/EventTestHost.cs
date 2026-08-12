using EventsApi.Application.Abstractions;
using EventsApi.Application.Services;
using EventsApi.Infrastructure.Persistence;
using EventsApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventsApi.Tests;

internal static class EventTestHost
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventService, EventService>();
        return services.BuildServiceProvider();
    }
}
