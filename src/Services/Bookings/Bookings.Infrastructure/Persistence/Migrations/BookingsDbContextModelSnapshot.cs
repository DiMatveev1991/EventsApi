using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bookings.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BookingsDbContext))]
public sealed class BookingsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.11");
        modelBuilder.Entity("Bookings.Domain.Entities.Booking", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uuid");
            entity.Property<DateTimeOffset?>("ConfirmationPublishedAt")
                .HasColumnType("timestamp with time zone");
            entity.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            entity.Property<Guid>("EventId").HasColumnType("uuid");
            entity.Property<DateTimeOffset?>("ProcessedAt").HasColumnType("timestamp with time zone");
            entity.Property<int>("Seats").HasColumnType("integer");
            entity.Property<string>("Status").IsRequired().HasMaxLength(20)
                .HasColumnType("character varying(20)");
            entity.Property<Guid>("UserId").HasColumnType("uuid");
            entity.HasKey("Id");
            entity.HasIndex("UserId");
            entity.HasIndex("Status", "ConfirmationPublishedAt");
            entity.ToTable("bookings");
        });
    }
}
