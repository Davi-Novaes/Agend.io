using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstUserRegisteredAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "first_user_registered_at_utc",
                schema: "tenancy",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill unico, so nesta migration: sem isto, todo tenant criado
            // ANTES desta coluna existir (mesmo os com dono ativo ha meses)
            // ficaria com first_user_registered_at_utc = null e seria tratado
            // como "abandonado" por OrphanTenantCleanupJob assim que completasse
            // 48h — perderia o slug e seria desativado por engano. Consulta
            // cross-schema aceitavel aqui porque e um script de migration/dado
            // historico rodando com a role admin do banco, nao codigo de
            // aplicacao em runtime (que continua proibido de ler tabela de
            // outro modulo — ver CLAUDE.md e Agendio.ArchitectureTests).
            //
            // Guardado por to_regclass: a suite de integracao migra os modulos
            // em ordem fixa (Tenancy ANTES de Identity, ver IntegrationTestFixture) —
            // num banco 100% novo, identity.users ainda nao existe neste ponto.
            // Em upgrade de producao (o caso real que importa) o schema identity
            // ja existe havia muito, entao o backfill roda normalmente.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('identity.users') IS NOT NULL THEN
                        UPDATE tenancy.tenants t
                        SET first_user_registered_at_utc = (
                            SELECT MIN(u.created_at_utc) FROM identity.users u WHERE u.tenant_id = t.id
                        )
                        WHERE EXISTS (SELECT 1 FROM identity.users u WHERE u.tenant_id = t.id);
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "first_user_registered_at_utc",
                schema: "tenancy",
                table: "tenants");
        }
    }
}
