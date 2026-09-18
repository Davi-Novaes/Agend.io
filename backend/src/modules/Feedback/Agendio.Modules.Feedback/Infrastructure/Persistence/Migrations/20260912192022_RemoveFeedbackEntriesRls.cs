using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Feedback.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFeedbackEntriesRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FeedbackEntry deixou de ser ITenantOwned (ver comentario na
            // entidade): o painel do Super Admin (Platform) precisa ler
            // feedback de todos os tenants, e agendio_owner/agendio_app sao
            // ambos NOBYPASSRLS -- mesmo raciocinio ja aplicado em
            // RemoveAuditLogRls (Identity/Platform/Customers).
            migrationBuilder.Sql("""
                DROP POLICY tenant_isolation ON feedback.feedback_entries;
                ALTER TABLE feedback.feedback_entries DISABLE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE feedback.feedback_entries ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON feedback.feedback_entries
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);
                """);
        }
    }
}
