using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartHotel.Booking.Infrastructure.Persistence;

#nullable disable

namespace SmartHotel.Booking.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(BookingDbContext))]
    [Migration("20260922180000_AddCheckoutInitiatedAtUtcAndBackfill")]
    public partial class AddCheckoutInitiatedAtUtcAndBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckoutInitiatedAtUtc",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Bookings\" SET \"CheckoutInitiatedAtUtc\" = \"CreatedAtUtc\" WHERE \"Status\" = 0 AND \"CheckoutInitiatedAtUtc\" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PendingPayment_CheckoutInitiated",
                table: "Bookings",
                column: "CheckoutInitiatedAtUtc",
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_PendingPayment_CheckoutInitiated",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CheckoutInitiatedAtUtc",
                table: "Bookings");
        }
    }
}
