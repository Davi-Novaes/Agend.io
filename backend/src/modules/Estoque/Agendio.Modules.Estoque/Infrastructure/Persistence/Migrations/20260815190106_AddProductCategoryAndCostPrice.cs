using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Estoque.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCategoryAndCostPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "estoque",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cost_price_amount",
                schema: "estoque",
                table: "products",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cost_price_currency",
                schema: "estoque",
                table: "products",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "category",
                schema: "estoque",
                table: "products");

            migrationBuilder.DropColumn(
                name: "cost_price_amount",
                schema: "estoque",
                table: "products");

            migrationBuilder.DropColumn(
                name: "cost_price_currency",
                schema: "estoque",
                table: "products");
        }
    }
}
