using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "mfa_enabled",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "mfa_secret_encrypted",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mfa_recovery_codes",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mfa_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mfa_recovery_codes_code_hash",
                schema: "identity",
                table: "mfa_recovery_codes",
                column: "code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mfa_recovery_codes_tenant_id_user_id",
                schema: "identity",
                table: "mfa_recovery_codes",
                columns: new[] { "tenant_id", "user_id" });

            // RLS padrao, sem excecao (diferente de refresh_tokens/team_invitations):
            // codigo de recuperacao so e consultado depois que o tenant ja foi
            // ancorado (challenge resolvido, ver VerifyMfaCommandHandler) — nunca
            // antes, entao nao precisa do sentinela de tenant vazio.
            migrationBuilder.Sql(
                """
                ALTER TABLE identity.mfa_recovery_codes ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON identity.mfa_recovery_codes
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mfa_recovery_codes",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "mfa_enabled",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_secret_encrypted",
                schema: "identity",
                table: "users");
        }
    }
}
