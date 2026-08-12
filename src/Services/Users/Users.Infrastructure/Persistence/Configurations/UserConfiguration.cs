using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Users.Domain.Entities;

namespace Users.Infrastructure.Persistence.Configurations;

/// <summary>Настраивает отображение пользователя в PostgreSQL.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>Настраивает таблицу, ограничения и уникальный индекс логина.</summary>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.Login).IsRequired().HasMaxLength(100);
        builder.HasIndex(user => user.Login).IsUnique();
        builder.Property(user => user.PasswordHash).IsRequired().HasMaxLength(256);
        builder.Property(user => user.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
    }
}
