using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApi.Infrastructure.Persistence.Migrations
{
    public partial class AddUsersAndBookingOwnership : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            // Служебный владелец нужен только для совместимости с бронями,
            // созданными до появления авторизации. Войти под ним невозможно.
            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "Login", "PasswordHash", "Role" },
                values: new object[]
                {
                    Guid.Empty,
                    "__legacy__",
                    "0000000000000000000000000000000000000000000000000000000000000000",
                    "User"
                });

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE bookings SET \"UserId\" = '00000000-0000-0000-0000-000000000000' WHERE \"UserId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "bookings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_UserId",
                table: "bookings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Login",
                table: "users",
                column: "Login",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_users_UserId",
                table: "bookings",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_users_UserId",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bookings_UserId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "bookings");

            migrationBuilder.DropTable(name: "users");
        }
    }
}
