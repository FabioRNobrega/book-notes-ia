using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageAudioAndVoicePreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoicePreference",
                table: "user_profile",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "female");

            migrationBuilder.CreateTable(
                name: "chat_message_audio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Voice = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_message_audio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chat_message_audio_chat_message_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalTable: "chat_message",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_audio_ChatMessageId",
                table: "chat_message_audio",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_audio_ChatMessageId_Language_Voice",
                table: "chat_message_audio",
                columns: new[] { "ChatMessageId", "Language", "Voice" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_message_audio");

            migrationBuilder.DropColumn(
                name: "VoicePreference",
                table: "user_profile");
        }
    }
}
