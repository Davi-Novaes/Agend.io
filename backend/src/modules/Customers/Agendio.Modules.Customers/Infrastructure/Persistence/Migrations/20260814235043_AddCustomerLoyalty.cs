using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Customers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "loyalty_points",
                schema: "customers",
                table: "customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "loyalty_points_ledger_entries",
                schema: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loyalty_points_ledger_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_points_ledger_entries_tenant_id_appointment_id",
                schema: "customers",
                table: "loyalty_points_ledger_entries",
                columns: new[] { "tenant_id", "appointment_id" },
                unique: true,
                filter: "kind = 'Earned'");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_points_ledger_entries_tenant_id_customer_id",
                schema: "customers",
                table: "loyalty_points_ledger_entries",
                columns: new[] { "tenant_id", "customer_id" });

            migrationBuilder.Sql(
                """
                ALTER TABLE customers.loyalty_points_ledger_entries ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON customers.loyalty_points_ledger_entries
                    USING (tenant_id = current_setting('app.tenant_id')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loyalty_points_ledger_entries",
                schema: "customers");

            migrationBuilder.DropColumn(
                name: "loyalty_points",
                schema: "customers",
                table: "customers");
        }
    }
}
