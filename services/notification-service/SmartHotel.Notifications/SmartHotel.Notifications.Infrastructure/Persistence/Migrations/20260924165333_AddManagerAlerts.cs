using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHotel.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manager_alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookingReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageSnippet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DismissedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActionedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manager_alerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manager_alerts_AlertType_Status",
                table: "manager_alerts",
                columns: new[] { "AlertType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_manager_alerts_EventId",
                table: "manager_alerts",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manager_alerts_Status_CreatedAt",
                table: "manager_alerts",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manager_alerts");
        }
    }
}
