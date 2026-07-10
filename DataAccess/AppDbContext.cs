using EventsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EventsApi.DataAccess
{
	/// <summary>
	/// Контекст EF Core для приложения. Предоставляет доступ к таблицам
	/// событий и бронирований и подключает конфигурации маппинга через Fluent API.
	/// </summary>
	public sealed class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

		public DbSet<Event> Events => Set<Event>();
		public DbSet<Booking> Bookings => Set<Booking>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			// Автоматически подключаем все IEntityTypeConfiguration<T> из сборки.
			modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
		}
	}
}
