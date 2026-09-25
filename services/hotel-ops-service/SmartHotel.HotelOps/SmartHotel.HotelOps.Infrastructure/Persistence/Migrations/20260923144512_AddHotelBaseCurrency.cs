using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHotel.HotelOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelBaseCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseCurrency",
                table: "Hotels",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "LKR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseCurrency",
                table: "Hotels");
        }
    }
}
