using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace SmartHotel.Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260917170000_AddDepartmentRoleSlots")]
public partial class AddDepartmentRoleSlots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DepartmentRoleSlot",
            schema: "identity",
            table: "Employees",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE identity."Employees" AS e
            SET "DepartmentRoleSlot" = e."Role" || ':' || e."DepartmentId"::text
            FROM identity."Persons" AS p
            WHERE p."Id" = e."Id"
              AND p."IsActive" = TRUE
              AND e."Status" = 'Active'
              AND e."Role" IN ('Admin', 'Manager');
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Employees_DepartmentRoleSlot",
            schema: "identity",
            table: "Employees",
            column: "DepartmentRoleSlot",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Employees_DepartmentRoleSlot",
            schema: "identity",
            table: "Employees");
        migrationBuilder.DropColumn(
            name: "DepartmentRoleSlot",
            schema: "identity",
            table: "Employees");
    }
}
