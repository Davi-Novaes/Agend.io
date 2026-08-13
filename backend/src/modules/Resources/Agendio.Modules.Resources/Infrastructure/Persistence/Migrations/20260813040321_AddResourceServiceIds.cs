using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Resources.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceServiceIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "service_ids",
                schema: "resources",
                table: "resources",
                type: "uuid[]",
                nullable: false,
                defaultValue: Array.Empty<Guid>());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "service_ids",
                schema: "resources",
                table: "resources");
        }
    }
}
