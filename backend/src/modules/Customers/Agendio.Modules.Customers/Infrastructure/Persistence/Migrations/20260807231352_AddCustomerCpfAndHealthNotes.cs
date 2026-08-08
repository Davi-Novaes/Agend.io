using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Customers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCpfAndHealthNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cpf",
                schema: "customers",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "health_notes",
                schema: "customers",
                table: "customers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cpf",
                schema: "customers",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "health_notes",
                schema: "customers",
                table: "customers");
        }
    }
}
