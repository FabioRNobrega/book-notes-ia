using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_profile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PreferredLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReadingLanguages = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    LearningStyle = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    LovedGenres = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    DislikedGenres = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    TonePreference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    LearningGoals = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FavoriteAuthors = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AboutMe = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    AgentProfileCompact = table.Column<string>(type: "text", nullable: false),
                    AgentProfileVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_profile_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_profile_UserId",
                table: "user_profile",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_profile");
        }
    }
}
