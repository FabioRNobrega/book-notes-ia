using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Migrations
{
    public partial class ChangeAgentProfileCompactToJsonb : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old text column (no data to preserve)
            migrationBuilder.DropColumn(
                name: "AgentProfileCompact",
                table: "user_profile");

            // Recreate as jsonb
            migrationBuilder.AddColumn<string>(
                name: "AgentProfileCompact",
                table: "user_profile",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentProfileCompact",
                table: "user_profile");

            migrationBuilder.AddColumn<string>(
                name: "AgentProfileCompact",
                table: "user_profile",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}