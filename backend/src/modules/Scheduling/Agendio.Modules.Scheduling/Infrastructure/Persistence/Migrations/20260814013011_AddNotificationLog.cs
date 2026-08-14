using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_log_entries",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sent_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_log_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_log_entries_tenant_id_appointment_id",
                schema: "scheduling",
                table: "notification_log_entries",
                columns: new[] { "tenant_id", "appointment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_log_entries_tenant_id_sent_at_utc",
                schema: "scheduling",
                table: "notification_log_entries",
                columns: new[] { "tenant_id", "sent_at_utc" });

            migrationBuilder.Sql(
                """
                ALTER TABLE scheduling.notification_log_entries ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON scheduling.notification_log_entries
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_log_entries",
                schema: "scheduling");
        }
    }
}
