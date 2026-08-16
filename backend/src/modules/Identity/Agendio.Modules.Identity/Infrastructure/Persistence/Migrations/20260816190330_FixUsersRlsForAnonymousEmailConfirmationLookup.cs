using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixUsersRlsForAnonymousEmailConfirmationLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ConfirmEmailCommandHandler localiza o usuario pelo HASH do token de
            // confirmacao ANTES de saber a qual tenant ele pertence (mesmo motivo
            // documentado em refresh_tokens/team_invitations) — app.tenant_id
            // ainda esta no sentinela vazio nesse instante. Sem esta excecao a
            // RLS bloqueava a leitura mesmo com hash correto, quebrando o fluxo
            // real de confirmacao (nao so um problema de teste). Seguro pelo
            // mesmo motivo: token_hash e SHA-256 de um segredo de 512 bits com
            // indice UNIQUE, nunca permite enumerar usuarios de outro tenant.
            migrationBuilder.Sql("""
                DROP POLICY tenant_isolation ON identity.users;
                CREATE POLICY tenant_isolation ON identity.users
                    USING (
                        tenant_id = current_setting('app.tenant_id')::uuid
                        OR current_setting('app.tenant_id')::uuid = '00000000-0000-0000-0000-000000000000'::uuid
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY tenant_isolation ON identity.users;
                CREATE POLICY tenant_isolation ON identity.users
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);
                """);
        }
    }
}
