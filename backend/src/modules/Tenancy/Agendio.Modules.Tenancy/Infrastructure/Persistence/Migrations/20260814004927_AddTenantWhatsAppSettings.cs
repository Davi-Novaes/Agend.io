using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantWhatsAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "whats_app_access_token",
                schema: "tenancy",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whats_app_cancelled_template",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whats_app_completed_template",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whats_app_confirmed_template",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "whats_app_integration_enabled",
                schema: "tenancy",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "whats_app_phone_number_id",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whats_app_reminder_template",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whats_app_rescheduled_template",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whats_app_scheduled_template",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "whats_app_access_token",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "whats_app_cancelled_template",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "whats_app_completed_template",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "whats_app_confirmed_template",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "whats_app_integration_enabled",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "whats_app_phone_number_id",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "whats_app_reminder_template",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "whats_app_rescheduled_template",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "whats_app_scheduled_template",
                schema: "tenancy",
                table: "tenants");
        }
    }
}
