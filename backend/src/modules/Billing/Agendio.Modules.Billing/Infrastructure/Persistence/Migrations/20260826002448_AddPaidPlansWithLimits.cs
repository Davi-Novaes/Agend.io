using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Agendio.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaidPlansWithLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_featured",
                schema: "billing",
                table: "plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "max_customers",
                schema: "billing",
                table: "plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_professionals",
                schema: "billing",
                table: "plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_units",
                schema: "billing",
                table: "plans",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "plans",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "is_active", "is_featured", "max_customers", "max_professionals", "max_units" },
                values: new object[] { false, false, null, null, null });

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "plans",
                keyColumn: "id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "is_featured", "max_customers", "max_professionals", "max_units" },
                values: new object[] { false, null, null, null });

            migrationBuilder.InsertData(
                schema: "billing",
                table: "plans",
                columns: new[] { "id", "billing_cycle", "currency", "is_active", "is_featured", "max_customers", "max_professionals", "max_units", "name", "price_amount" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333333"), "Monthly", "BRL", true, false, 300, 3, 1, "Essencial", 49.99m },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "Monthly", "BRL", true, true, 1500, 10, 3, "Profissional", 69.99m },
                    { new Guid("55555555-5555-5555-5555-555555555555"), "Monthly", "BRL", true, false, null, 30, 10, "Premium", 99.99m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "billing",
                table: "plans",
                keyColumn: "id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            migrationBuilder.DeleteData(
                schema: "billing",
                table: "plans",
                keyColumn: "id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                schema: "billing",
                table: "plans",
                keyColumn: "id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"));

            migrationBuilder.DropColumn(
                name: "is_featured",
                schema: "billing",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "max_customers",
                schema: "billing",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "max_professionals",
                schema: "billing",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "max_units",
                schema: "billing",
                table: "plans");

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "plans",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "is_active",
                value: true);
        }
    }
}
