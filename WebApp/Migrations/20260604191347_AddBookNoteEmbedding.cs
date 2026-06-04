using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace WebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddBookNoteEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "book_note_embedding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_note_embedding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_book_note_embedding_book_BookId",
                        column: x => x.BookId,
                        principalTable: "book",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_note_embedding_book_note_BookNoteId",
                        column: x => x.BookNoteId,
                        principalTable: "book_note",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_book_note_embedding_BookId",
                table: "book_note_embedding",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_book_note_embedding_BookNoteId",
                table: "book_note_embedding",
                column: "BookNoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_book_note_embedding_Embedding",
                table: "book_note_embedding",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_book_note_embedding_UserId",
                table: "book_note_embedding",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_book_note_embedding_UserId_BookId",
                table: "book_note_embedding",
                columns: new[] { "UserId", "BookId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_note_embedding");
        }
    }
}
