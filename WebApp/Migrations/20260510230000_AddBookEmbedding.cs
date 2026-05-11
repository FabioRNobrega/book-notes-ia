using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace WebApp.Migrations
{
    public partial class AddBookEmbedding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.CreateTable(
                name: "book_embedding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_embedding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_book_embedding_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_embedding_book_BookId",
                        column: x => x.BookId,
                        principalTable: "book",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_book_embedding_BookId",
                table: "book_embedding",
                column: "BookId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_book_embedding_UserId",
                table: "book_embedding",
                column: "UserId");

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_book_embedding_Embedding"
                ON book_embedding
                USING hnsw ("Embedding" vector_cosine_ops);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_embedding");
        }
    }
}
