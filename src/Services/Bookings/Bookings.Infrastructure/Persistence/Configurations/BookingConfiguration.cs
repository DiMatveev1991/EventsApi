using Bookings.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookings.Infrastructure.Persistence.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");
        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Id).ValueGeneratedNever();
        builder.Property(booking => booking.EventId).IsRequired();
        builder.Property(booking => booking.UserId).IsRequired();
        builder.Property(booking => booking.Seats).IsRequired();
        builder.Property(booking => booking.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(booking => booking.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");
        builder.Property(booking => booking.ProcessedAt)
            .HasColumnType("timestamp with time zone");
        builder.Property(booking => booking.ConfirmationPublishedAt)
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(booking => booking.UserId);
        builder.HasIndex(booking => new { booking.Status, booking.ConfirmationPublishedAt });
    }
}
