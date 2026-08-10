using EventsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsApi.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Конфигурация маппинга сущности <see cref="Event"/> на таблицу events.
    /// </summary>
    public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
    {
        public void Configure(EntityTypeBuilder<Event> builder)
        {
            builder.ToTable("events");

            builder.HasKey(e => e.Id);

            // Идентификатор генерируется в коде (Guid.NewGuid), а не базой данных.
            builder.Property(e => e.Id)
                .ValueGeneratedNever();

            builder.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.Description)
                .HasMaxLength(2000);

            // Время события — абсолютный момент с явным смещением. PostgreSQL
            // нормализует timestamptz в UTC и не теряет смысл ISO 8601 значений с Z.
            builder.Property(e => e.StartAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            builder.Property(e => e.EndAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            builder.Property(e => e.TotalSeats)
                .IsRequired();

            builder.Property(e => e.AvailableSeats)
                .IsRequired();

            // Связь «один–ко–многим» с бронированиями через навигационные свойства.
            builder.HasMany(e => e.Bookings)
                .WithOne(b => b.Event)
                .HasForeignKey(b => b.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
