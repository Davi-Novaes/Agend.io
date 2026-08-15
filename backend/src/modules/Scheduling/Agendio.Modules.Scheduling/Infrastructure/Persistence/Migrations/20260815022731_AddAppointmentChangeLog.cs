using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_change_log_entries",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    change_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    previous_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    new_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    by_staff = table.Column<bool>(type: "boolean", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_change_log_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_change_log_entries_tenant_id_appointment_id_occ",
                schema: "scheduling",
                table: "appointment_change_log_entries",
                columns: new[] { "tenant_id", "appointment_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_change_log_entries_tenant_id_occurred_at_utc",
                schema: "scheduling",
                table: "appointment_change_log_entries",
                columns: new[] { "tenant_id", "occurred_at_utc" });

            migrationBuilder.Sql(
                """
                ALTER TABLE scheduling.appointment_change_log_entries ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON scheduling.appointment_change_log_entries
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_change_log_entries",
                schema: "scheduling");
        }
    }
}
