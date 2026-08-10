using EventsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsApi.Infrastructure.Persistence.Configurations;

/// <summary>Конфигурация таблицы пользователей.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Login)
            .IsRequired()
            .HasMaxLength(100);
        builder.HasIndex(user => user.Login).IsUnique();

        builder.Property(user => user.PasswordHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(user => user.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasMany(user => user.Bookings)
            .WithOne(booking => booking.User)
            .HasForeignKey(booking => booking.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
