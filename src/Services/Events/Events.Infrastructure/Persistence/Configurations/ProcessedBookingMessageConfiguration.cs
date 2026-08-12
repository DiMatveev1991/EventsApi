using EventsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsApi.Infrastructure.Persistence.Configurations;

/// <summary>Настраивает inbox-таблицу обработанных подтверждений бронирований.</summary>
public sealed class ProcessedBookingMessageConfiguration
    : IEntityTypeConfiguration<ProcessedBookingMessage>
{
    /// <summary>Настраивает первичный ключ и поля inbox-маркера.</summary>
    public void Configure(EntityTypeBuilder<ProcessedBookingMessage> builder)
    {
        builder.ToTable("processed_booking_messages");
        builder.HasKey(message => message.BookingId);
        builder.Property(message => message.BookingId).ValueGeneratedNever();
        builder.Property(message => message.ProcessedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(message => message.Result).HasMaxLength(40).IsRequired();
    }
}
