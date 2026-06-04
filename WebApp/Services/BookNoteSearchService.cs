using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using WebApp.Models;

namespace WebApp.Services;

public interface IBookNoteSearchService
{
    Task<IReadOnlyList<BookNote>> SearchAsync(Book book, string searchQuery, string userId, CancellationToken ct = default);
}

public sealed class BookNoteSearchService(
    AppDbContext db,
    IEmbeddingService embeddingService,
    IConfiguration configuration) : IBookNoteSearchService
{
    private int TopK => configuration.GetValue<int?>("BookNotes:TopKRelevantNotes") ?? 20;

    public async Task<IReadOnlyList<BookNote>> SearchAsync(Book book, string searchQuery, string userId, CancellationToken ct = default)
    {
        var queryVector = await embeddingService.EmbedAsync(searchQuery, ct);
        var vectorLiteral = ToVectorLiteral(queryVector);

        var userIdParam = new NpgsqlParameter("user_id", userId);
        var bookIdParam = new NpgsqlParameter("book_id", book.Id);
        var vectorParam = new NpgsqlParameter("query_vector", vectorLiteral);
        var topKParam = new NpgsqlParameter("top_k", TopK);

        var closestIds = await db.Database
            .SqlQueryRaw<ClosestNoteEmbedding>(
                """
                SELECT "BookNoteId", "Embedding" <=> @query_vector::vector AS "Distance"
                FROM book_note_embedding
                WHERE "UserId" = @user_id AND "BookId" = @book_id
                ORDER BY "Embedding" <=> @query_vector::vector
                LIMIT @top_k
                """,
                userIdParam,
                bookIdParam,
                vectorParam,
                topKParam)
            .ToListAsync(ct);

        if (closestIds.Count == 0)
            return [];

        var noteIds = closestIds.Select(x => x.BookNoteId).ToList();

        var notes = await db.BookNotes
            .AsNoTracking()
            .Where(n => noteIds.Contains(n.Id) && n.UserId == userId)
            .ToListAsync(ct);

        return noteIds
            .Select(id => notes.FirstOrDefault(n => n.Id == id))
            .OfType<BookNote>()
            .ToList();
    }

    private static string ToVectorLiteral(float[] values) =>
        $"[{string.Join(",", values.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))}]";

    private sealed class ClosestNoteEmbedding
    {
        public Guid BookNoteId { get; set; }
        public double Distance { get; set; }
    }
}
