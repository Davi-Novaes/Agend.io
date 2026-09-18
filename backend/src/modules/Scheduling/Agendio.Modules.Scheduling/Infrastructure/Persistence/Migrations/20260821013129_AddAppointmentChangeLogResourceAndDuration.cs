using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Scheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentChangeLogResourceAndDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "new_end_utc",
                schema: "scheduling",
                table: "appointment_change_log_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "previous_resource_id",
                schema: "scheduling",
                table: "appointment_change_log_entries",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "new_end_utc",
                schema: "scheduling",
                table: "appointment_change_log_entries");

            migrationBuilder.DropColumn(
                name: "previous_resource_id",
                schema: "scheduling",
                table: "appointment_change_log_entries");
        }
    }
}
