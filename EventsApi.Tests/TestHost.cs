using EventsApi.DataAccess;
using EventsApi.Repositories;
using EventsApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventsApi.Tests;

/// <summary>
/// Строит изолированный DI-контейнер для юнит-тестов: EF Core с InMemory-провайдером
/// плюс сервисы приложения. Каждый вызов создаёт уникальную InMemory-базу, чтобы
/// тесты не влияли друг на друга.
/// </summary>
internal static class TestHost
{
	public static ServiceProvider Build()
	{
		// Имя базы выносим в переменную: если вызвать Guid.NewGuid() прямо в лямбде,
		// каждый scope получит новую базу и данные не будут общими.
		var dbName = Guid.NewGuid().ToString();

		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDbContext<AppDbContext>(options =>
			options.UseInMemoryDatabase(dbName));
		services.AddScoped<IEventRepository, EventRepository>();
		services.AddScoped<IBookingRepository, BookingRepository>();
		services.AddScoped<IEventService, EventService>();
		services.AddScoped<IBookingService, BookingService>();

		return services.BuildServiceProvider();
	}
}
