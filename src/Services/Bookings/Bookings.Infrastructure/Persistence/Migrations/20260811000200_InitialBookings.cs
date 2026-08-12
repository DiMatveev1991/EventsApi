using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bookings.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BookingsDbContext))]
[Migration("20260811000200_InitialBookings")]
public sealed class InitialBookings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bookings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Seats = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ConfirmationPublishedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_bookings", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_bookings_UserId",
            table: "bookings",
            column: "UserId");
        migrationBuilder.CreateIndex(
            name: "IX_bookings_Status_ConfirmationPublishedAt",
            table: "bookings",
            columns: new[] { "Status", "ConfirmationPublishedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "bookings");
}
