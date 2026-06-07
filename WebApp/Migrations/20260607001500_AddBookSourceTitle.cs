using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddBookSourceTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceBookTitle",
                table: "book",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE book
                SET "SourceBookTitle" = "Title"
                WHERE "SourceBookTitle" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_book_UserId_SourceBookTitle_NormalizedAuthor",
                table: "book",
                columns: new[] { "UserId", "SourceBookTitle", "NormalizedAuthor" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_book_UserId_SourceBookTitle_NormalizedAuthor",
                table: "book");

            migrationBuilder.DropColumn(
                name: "SourceBookTitle",
                table: "book");
        }
    }
}
