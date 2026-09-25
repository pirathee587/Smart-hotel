using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHotel.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingAndDraftCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Bookings",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "BookingDrafts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "BookingDrafts");
        }
    }
}
