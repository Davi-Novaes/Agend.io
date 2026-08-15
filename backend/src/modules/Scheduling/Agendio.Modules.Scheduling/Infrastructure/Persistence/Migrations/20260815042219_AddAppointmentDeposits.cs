using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentDeposits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_deposits",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gateway_charge_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    invoice_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    paid_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_deposits", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_deposits_appointment_id",
                schema: "scheduling",
                table: "appointment_deposits",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointment_deposits_tenant_id_gateway_charge_id",
                schema: "scheduling",
                table: "appointment_deposits",
                columns: new[] { "tenant_id", "gateway_charge_id" });

            migrationBuilder.Sql(
                """
                ALTER TABLE scheduling.appointment_deposits ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON scheduling.appointment_deposits
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_deposits",
                schema: "scheduling");
        }
    }
}
