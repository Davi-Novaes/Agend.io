using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "team_invitations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_invitations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_team_invitations_tenant_id_email",
                schema: "identity",
                table: "team_invitations",
                columns: new[] { "tenant_id", "email" });

            migrationBuilder.CreateIndex(
                name: "ix_team_invitations_token_hash",
                schema: "identity",
                table: "team_invitations",
                column: "token_hash",
                unique: true);

            // Mesma excecao deliberada de refresh_tokens (ver InitialCreate): o
            // fluxo de aceitar convite localiza o registro pelo HASH do token
            // ANTES de saber a qual tenant ele pertence, entao app.tenant_id
            // ainda esta no sentinela vazio nesse instante. Seguro pelo mesmo
            // motivo: token_hash e SHA-256 de um segredo de 512 bits com indice
            // UNIQUE, nunca permite enumerar convites de outro tenant.
            migrationBuilder.Sql(
                """
                ALTER TABLE identity.team_invitations ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON identity.team_invitations
                    USING (
                        tenant_id = current_setting('app.tenant_id')::uuid
                        OR current_setting('app.tenant_id')::uuid = '00000000-0000-0000-0000-000000000000'::uuid
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_invitations",
                schema: "identity");
        }
    }
}
