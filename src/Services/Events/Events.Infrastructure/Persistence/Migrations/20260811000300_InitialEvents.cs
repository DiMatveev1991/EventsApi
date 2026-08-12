using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EventsApi.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260811000300_InitialEvents")]
public sealed class InitialEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false),
                Description = table.Column<string>(
                    type: "character varying(2000)",
                    maxLength: 2000,
                    nullable: true),
                StartAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                EndAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                TotalSeats = table.Column<int>(type: "integer", nullable: false),
                AvailableSeats = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_events", x => x.Id));

        migrationBuilder.CreateTable(
            name: "processed_booking_messages",
            columns: table => new
            {
                BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false),
                Result = table.Column<string>(
                    type: "character varying(40)",
                    maxLength: 40,
                    nullable: false)
            },
            constraints: table =>
                table.PrimaryKey("PK_processed_booking_messages", x => x.BookingId));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "processed_booking_messages");
        migrationBuilder.DropTable(name: "events");
    }
}
