using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Customers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerEmailUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_customers_tenant_id_email",
                schema: "customers",
                table: "customers",
                columns: new[] { "tenant_id", "email" },
                unique: true,
                filter: "email IS NOT NULL AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_customers_tenant_id_email",
                schema: "customers",
                table: "customers");
        }
    }
}
