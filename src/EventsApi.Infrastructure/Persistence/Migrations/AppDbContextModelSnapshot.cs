using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EventsApi.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
public sealed class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.11");
        modelBuilder.Entity("EventsApi.Domain.Entities.Event", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uuid");
            entity.Property<int>("AvailableSeats").HasColumnType("integer");
            entity.Property<string>("Description").HasMaxLength(2000)
                .HasColumnType("character varying(2000)");
            entity.Property<DateTimeOffset>("EndAt").HasColumnType("timestamp with time zone");
            entity.Property<DateTimeOffset>("StartAt").HasColumnType("timestamp with time zone");
            entity.Property<string>("Title").IsRequired().HasMaxLength(200)
                .HasColumnType("character varying(200)");
            entity.Property<int>("TotalSeats").HasColumnType("integer");
            entity.HasKey("Id");
            entity.ToTable("events");
        });
        modelBuilder.Entity("EventsApi.Domain.Entities.ProcessedBookingMessage", entity =>
        {
            entity.Property<Guid>("BookingId").ValueGeneratedNever().HasColumnType("uuid");
            entity.Property<DateTimeOffset>("ProcessedAt")
                .HasColumnType("timestamp with time zone");
            entity.Property<string>("Result").IsRequired().HasMaxLength(40)
                .HasColumnType("character varying(40)");
            entity.HasKey("BookingId");
            entity.ToTable("processed_booking_messages");
        });
    }
}
