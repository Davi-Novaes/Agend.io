using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Assistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "assistant");

            migrationBuilder.CreateTable(
                name: "assistant_query_log",
                schema: "assistant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_excerpt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    resolved_intent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tool_or_service_used = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assistant_query_log", x => x.id);
                });

            // RLS: agendio_owner e agendio_app conectam SEM BYPASSRLS (defesa em
            // profundidade, ver CLAUDE.md) — mesmo padrao exato dos outros modulos
            // (ver Feedback/Customers InitialCreate).
            migrationBuilder.Sql(
                """
                ALTER TABLE assistant.assistant_query_log ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON assistant.assistant_query_log
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);
                """);

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "assistant",
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
                schema: "assistant",
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

            migrationBuilder.CreateIndex(
                name: "ix_assistant_query_log_tenant_id_occurred_at_utc",
                schema: "assistant",
                table: "assistant_query_log",
                columns: new[] { "tenant_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_occurred_at_utc",
                schema: "assistant",
                table: "audit_log",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_tenant_id_entity_type_entity_id",
                schema: "assistant",
                table: "audit_log",
                columns: new[] { "tenant_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on_utc",
                schema: "assistant",
                table: "outbox_messages",
                column: "processed_on_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assistant_query_log",
                schema: "assistant");

            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "assistant");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "assistant");
        }
    }
}
