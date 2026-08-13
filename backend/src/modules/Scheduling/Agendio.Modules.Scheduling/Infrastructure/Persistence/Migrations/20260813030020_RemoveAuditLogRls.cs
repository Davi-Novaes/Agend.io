using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAuditLogRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Padroniza com os modulos mais novos (Estoque/Financeiro/Marketing):
            // audit_log nunca tem RLS. O AuditLogInterceptor ja garante TenantId
            // nao-nulo em toda linha (ver Agendio.Infrastructure), entao a RLS
            // aqui era redundante e cegava o painel de reconciliacao do Super
            // Admin (mesmo raciocinio ja aplicado a outbox_messages).
            migrationBuilder.Sql("""
                DROP POLICY tenant_isolation ON scheduling.audit_log;
                ALTER TABLE scheduling.audit_log DISABLE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE scheduling.audit_log ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON scheduling.audit_log
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);
                """);
        }
    }
}
