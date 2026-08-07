using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scheduling");

            migrationBuilder.CreateTable(
                name: "appointments",
                schema: "scheduling",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    start_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "scheduling",
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
                name: "ix_appointments_tenant_id_resource_id_customer_id",
                schema: "scheduling",
                table: "appointments",
                columns: new[] { "tenant_id", "resource_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on_utc",
                schema: "scheduling",
                table: "outbox_messages",
                column: "processed_on_utc");

            // Coluna gerada pelo Postgres a partir de start_at_utc/end_at_utc — o
            // EF nunca le nem escreve nela, ela so existe para a EXCLUDE
            // constraint abaixo poder comparar intervalos com o operador &&.
            // btree_gist (extensao habilitada no Sprint 0) e o que permite usar
            // "=" em colunas uuid dentro de um indice GIST.
            migrationBuilder.Sql(
                """
                ALTER TABLE scheduling.appointments
                    ADD COLUMN time_range tstzrange GENERATED ALWAYS AS (tstzrange(start_at_utc, end_at_utc, '[)')) STORED;
                """);

            // A garantia de "sem overbooking" mora AQUI, nao na aplicacao: duas
            // requisicoes simultaneas tentando reservar o mesmo recurso no mesmo
            // horario sempre resultam em exatamente um INSERT bem-sucedido — o
            // Postgres rejeita o segundo com 23P01 (exclusion_violation), que o
            // ScheduleAppointmentCommandHandler traduz para "Appointment.SlotTaken".
            // So bloqueia contra status ativos: um agendamento cancelado ou
            // concluido nao ocupa mais o horario.
            migrationBuilder.Sql(
                """
                ALTER TABLE scheduling.appointments
                    ADD CONSTRAINT ex_appointments_no_overlap
                    EXCLUDE USING gist (
                        tenant_id WITH =,
                        resource_id WITH =,
                        time_range WITH &&
                    ) WHERE (status IN ('Scheduled', 'Confirmed', 'InProgress'));
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE scheduling.appointments ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON scheduling.appointments
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointments",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "scheduling");
        }
    }
}
