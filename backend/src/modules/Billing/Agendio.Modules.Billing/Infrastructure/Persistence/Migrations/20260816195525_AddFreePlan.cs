using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFreePlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "billing",
                table: "plans",
                columns: new[] { "id", "billing_cycle", "currency", "is_active", "name", "price_amount" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), "Monthly", "BRL", true, "Grátis", 0.00m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "billing",
                table: "plans",
                keyColumn: "id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));
        }
    }
}
