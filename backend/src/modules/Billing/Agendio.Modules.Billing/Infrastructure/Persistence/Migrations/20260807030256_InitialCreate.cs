using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // billing.subscriptions e billing.payments NAO tem RLS de proposito
            // (nao sao ITenantOwned) — ver comentario em Subscription.cs. Nao e
            // um passo esquecido: agendio_owner e agendio_app sao ambos
            // NOBYPASSRLS, entao RLS aqui deixaria o painel Super Admin e o job
            // de conciliacao cegos para todo tenant, sempre.
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    before = table.Column<string>(type: "jsonb", nullable: true),
                    after = table.Column<string>(type: "jsonb", nullable: true),
                    performed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asaas_payment_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    paid_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invoice_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    billing_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    billing_cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    trial_ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    asaas_customer_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    asaas_subscription_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    current_period_ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    canceled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "billing",
                table: "plans",
                columns: new[] { "id", "billing_cycle", "currency", "is_active", "name", "price_amount" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "Monthly", "BRL", true, "Padrão", 99.00m });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_occurred_at_utc",
                schema: "billing",
                table: "audit_log",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_tenant_id_entity_type_entity_id",
                schema: "billing",
                table: "audit_log",
                columns: new[] { "tenant_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on_utc",
                schema: "billing",
                table: "outbox_messages",
                column: "processed_on_utc");

            migrationBuilder.CreateIndex(
                name: "ix_payments_asaas_payment_id",
                schema: "billing",
                table: "payments",
                column: "asaas_payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id",
                schema: "billing",
                table: "payments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_tenant_id",
                schema: "billing",
                table: "subscriptions",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "plans",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "billing");
        }
    }
}
