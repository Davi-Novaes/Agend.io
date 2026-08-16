using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendio.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_confirmation_token_expires_at_utc",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email_confirmation_token_hash",
                schema: "identity",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_confirmed_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email_confirmation_token_hash",
                schema: "identity",
                table: "users",
                column: "email_confirmation_token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_email_confirmation_token_hash",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "email_confirmation_token_expires_at_utc",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "email_confirmation_token_hash",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "email_confirmed_at",
                schema: "identity",
                table: "users");
        }
    }
}
