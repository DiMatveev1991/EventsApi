using EventsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsApi.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Конфигурация маппинга сущности <see cref="Booking"/> на таблицу bookings.
    /// </summary>
    public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("bookings");

            builder.HasKey(b => b.Id);

            // Идентификатор генерируется в коде (Guid.NewGuid), а не базой данных.
            builder.Property(b => b.Id)
                .ValueGeneratedNever();

            builder.Property(b => b.EventId)
                .IsRequired();

            builder.Property(b => b.UserId)
                .IsRequired();

            // Статус храним в БД строкой ("Pending"/"Confirmed"/"Rejected"),
            // а не числом — читаемее при просмотре таблицы.
            builder.Property(b => b.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            // Время создания и обработки брони — это UTC-моменты (DateTime.UtcNow),
            // поэтому храним их в timestamp with time zone.
            builder.Property(b => b.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            builder.Property(b => b.ProcessedAt)
                .HasColumnType("timestamp with time zone");

            // Связь с событием: внешний ключ EventId, множество броней у события.
            builder.HasOne(b => b.Event)
                .WithMany(e => e.Bookings)
                .HasForeignKey(b => b.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
