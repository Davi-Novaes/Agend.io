using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Resources.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceBusinessHoursInheritance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "inherits_business_hours",
                schema: "resources",
                table: "resources",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // O painel antigo gravava automaticamente segunda a sabado, das
            // 09h as 18h, mesmo sem o dono personalizar a jornada. Essa forma
            // exata passa a herdar o expediente da empresa; qualquer escala
            // diferente foi configurada de proposito e continua personalizada.
            migrationBuilder.Sql("""
                UPDATE resources.resources AS r
                SET inherits_business_hours = FALSE
                WHERE EXISTS (
                    SELECT 1
                    FROM resources.resource_working_hours AS h
                    WHERE h.resource_id = r.id
                )
                AND NOT (
                    SELECT COUNT(*) = 6
                        AND COUNT(DISTINCT h.day_of_week) = 6
                        AND BOOL_AND(h.day_of_week IN ('Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'))
                        AND BOOL_AND(h.start_time = TIME '09:00:00')
                        AND BOOL_AND(h.end_time = TIME '18:00:00')
                    FROM resources.resource_working_hours AS h
                    WHERE h.resource_id = r.id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "inherits_business_hours",
                schema: "resources",
                table: "resources");
        }
    }
}
