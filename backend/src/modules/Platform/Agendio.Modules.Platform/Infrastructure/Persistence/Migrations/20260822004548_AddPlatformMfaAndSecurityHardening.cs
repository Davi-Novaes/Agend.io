using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformMfaAndSecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempt_count",
                schema: "platform",
                table: "platform_admins",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until_utc",
                schema: "platform",
                table: "platform_admins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mfa_enabled",
                schema: "platform",
                table: "platform_admins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "mfa_secret_encrypted",
                schema: "platform",
                table: "platform_admins",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "security_audit_log",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_audit_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_log_actor_id_occurred_at_utc",
                schema: "platform",
                table: "security_audit_log",
                columns: new[] { "actor_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_log_event_type",
                schema: "platform",
                table: "security_audit_log",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_log_tenant_id_occurred_at_utc",
                schema: "platform",
                table: "security_audit_log",
                columns: new[] { "tenant_id", "occurred_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_audit_log",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "failed_login_attempt_count",
                schema: "platform",
                table: "platform_admins");

            migrationBuilder.DropColumn(
                name: "locked_until_utc",
                schema: "platform",
                table: "platform_admins");

            migrationBuilder.DropColumn(
                name: "mfa_enabled",
                schema: "platform",
                table: "platform_admins");

            migrationBuilder.DropColumn(
                name: "mfa_secret_encrypted",
                schema: "platform",
                table: "platform_admins");
        }
    }
}
