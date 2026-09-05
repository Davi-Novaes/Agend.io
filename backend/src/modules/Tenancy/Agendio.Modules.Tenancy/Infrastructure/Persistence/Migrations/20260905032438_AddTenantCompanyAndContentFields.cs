using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCompanyAndContentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "booking_instructions_text",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "home_cta_text",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "home_hero_description",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "home_hero_title",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legal_name",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "public_page_enabled",
                schema: "tenancy",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zip_code",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "booking_instructions_text",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "city",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "document",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "home_cta_text",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "home_hero_description",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "home_hero_title",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "legal_name",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "public_page_enabled",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "state",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "zip_code",
                schema: "tenancy",
                table: "tenants");
        }
    }
}
