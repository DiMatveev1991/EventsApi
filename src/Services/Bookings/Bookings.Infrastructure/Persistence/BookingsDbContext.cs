using Bookings.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookings.Infrastructure.Persistence;

/// <summary>Контекст EF Core для базы данных бронирований.</summary>
public sealed class BookingsDbContext(DbContextOptions<BookingsDbContext> options) : DbContext(options)
{
    /// <summary>Набор бронирований.</summary>
    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>Подключает конфигурации сущностей текущей сборки.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingsDbContext).Assembly);
}
