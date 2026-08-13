using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Resources.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourcePhotoAndSpecialties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                schema: "resources",
                table: "resources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "specialties",
                schema: "resources",
                table: "resources",
                type: "character varying(100)[]",
                nullable: false,
                defaultValue: Array.Empty<string>());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_url",
                schema: "resources",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "specialties",
                schema: "resources",
                table: "resources");
        }
    }
}
