using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Marketing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignChannelAndTargetSegment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "channel",
                schema: "marketing",
                table: "campaigns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Email");

            migrationBuilder.AddColumn<string>(
                name: "target_segment",
                schema: "marketing",
                table: "campaigns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "channel",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "target_segment",
                schema: "marketing",
                table: "campaigns");
        }
    }
}
