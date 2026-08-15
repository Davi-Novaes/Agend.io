using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPaymentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "deposit_percentage",
                schema: "tenancy",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "payment_requirement",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deposit_percentage",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "payment_requirement",
                schema: "tenancy",
                table: "tenants");
        }
    }
}
