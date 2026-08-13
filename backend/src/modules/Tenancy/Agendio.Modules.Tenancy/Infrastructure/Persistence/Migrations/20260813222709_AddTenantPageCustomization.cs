using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPageCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "button_style",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Rounded");

            migrationBuilder.AddColumn<string>(
                name: "font",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Default");

            migrationBuilder.AddColumn<string>(
                name: "secondary_color_hex",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_about_section",
                schema: "tenancy",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_contact_section",
                schema: "tenancy",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_hours_section",
                schema: "tenancy",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_services_section",
                schema: "tenancy",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_team_section",
                schema: "tenancy",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "button_style",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "font",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "secondary_color_hex",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "show_about_section",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "show_contact_section",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "show_hours_section",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "show_services_section",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "show_team_section",
                schema: "tenancy",
                table: "tenants");
        }
    }
}
