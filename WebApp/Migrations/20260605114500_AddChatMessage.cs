using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_message",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalInputTokensProcessed = table.Column<int>(type: "integer", nullable: true),
                    TotalOutputTokensGenerated = table.Column<int>(type: "integer", nullable: true),
                    LatestPromptTokens = table.Column<int>(type: "integer", nullable: true),
                    MaxPromptTokens = table.Column<int>(type: "integer", nullable: true),
                    ContextUsagePct = table.Column<int>(type: "integer", nullable: true),
                    ModelCallCount = table.Column<int>(type: "integer", nullable: true),
                    ResponseTimeMs = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_message", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_UserId_SessionId_DisplayOrder",
                table: "chat_message",
                columns: new[] { "UserId", "SessionId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_message");
        }
    }
}
