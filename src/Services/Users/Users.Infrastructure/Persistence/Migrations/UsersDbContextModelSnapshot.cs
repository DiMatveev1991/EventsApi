using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Users.Infrastructure.Persistence.Migrations;

[DbContext(typeof(UsersDbContext))]
public sealed class UsersDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.11");
        modelBuilder.Entity("Users.Domain.Entities.User", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uuid");
            entity.Property<string>("Login").IsRequired().HasMaxLength(100)
                .HasColumnType("character varying(100)");
            entity.Property<string>("PasswordHash").IsRequired().HasMaxLength(256)
                .HasColumnType("character varying(256)");
            entity.Property<string>("Role").IsRequired().HasMaxLength(20)
                .HasColumnType("character varying(20)");
            entity.HasKey("Id");
            entity.HasIndex("Login").IsUnique();
            entity.ToTable("users");
        });
    }
}
