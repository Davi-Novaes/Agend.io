using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "facebook_url",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instagram_url",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "whats_app",
                schema: "tenancy",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_business_hours",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    day_of_week = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_business_hours", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_business_hours_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_business_hours_tenant_id",
                schema: "tenancy",
                table: "tenant_business_hours",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_business_hours",
                schema: "tenancy");

            migrationBuilder.DropColumn(
                name: "address",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "facebook_url",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "instagram_url",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "tenancy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "whats_app",
                schema: "tenancy",
                table: "tenants");
        }
    }
}
