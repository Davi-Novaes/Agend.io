using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantLoyaltySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "loyalty_program_enabled",
                schema: "tenancy",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "loyalty_reward_description",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Um agradecimento especial");

            migrationBuilder.AddColumn<int>(
                name: "loyalty_visits_for_reward",
                schema: "tenancy",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "loyalty_program_enabled",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "loyalty_reward_description",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "loyalty_visits_for_reward",
                schema: "tenancy",
                table: "tenants");
        }
    }
}
