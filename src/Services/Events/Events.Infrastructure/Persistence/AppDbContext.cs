using EventsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventsApi.Infrastructure.Persistence
{
    /// <summary>
    /// Контекст EF Core для приложения. Предоставляет доступ к таблицам
    /// событий и бронирований и подключает конфигурации маппинга через Fluent API.
    /// </summary>
    public sealed class AppDbContext : DbContext
    {
        /// <summary>Создаёт контекст с настройками, переданными composition root.</summary>
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        /// <summary>Набор событий.</summary>
        public DbSet<Event> Events => Set<Event>();

        /// <summary>Inbox обработанных подтверждений бронирований.</summary>
        public DbSet<ProcessedBookingMessage> ProcessedBookingMessages =>
            Set<ProcessedBookingMessage>();

        /// <summary>Подключает конфигурации сущностей текущей сборки.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Автоматически подключаем все IEntityTypeConfiguration<T> из сборки.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
