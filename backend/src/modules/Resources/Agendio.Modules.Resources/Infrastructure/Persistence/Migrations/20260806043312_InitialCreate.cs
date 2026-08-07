using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agendio.Modules.Resources.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "resources");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "resources",
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
                name: "resources",
                schema: "resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "resource_working_hours",
                schema: "resources",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    day_of_week = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_working_hours", x => x.id);
                    table.ForeignKey(
                        name: "fk_resource_working_hours_resources_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "resources",
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on_utc",
                schema: "resources",
                table: "outbox_messages",
                column: "processed_on_utc");

            migrationBuilder.CreateIndex(
                name: "ix_resource_working_hours_resource_id",
                schema: "resources",
                table: "resource_working_hours",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_resources_tenant_id_name",
                schema: "resources",
                table: "resources",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.Sql(
                """
                ALTER TABLE resources.resources ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON resources.resources
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);

                -- resource_working_hours nao tem tenant_id proprio (e um Value
                -- Object owned, sem identidade de tenant direta) — a politica
                -- verifica o tenant do recurso dono via subquery, pra RLS
                -- continuar protegendo mesmo se um bug futuro consultar essa
                -- tabela sem passar pelo agregado Resource.
                ALTER TABLE resources.resource_working_hours ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON resources.resource_working_hours
                    USING (EXISTS (
                        SELECT 1 FROM resources.resources r
                        WHERE r.id = resource_working_hours.resource_id
                          AND r.tenant_id = current_setting('app.tenant_id')::uuid
                    ));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "resource_working_hours",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "resources",
                schema: "resources");
        }
    }
}
