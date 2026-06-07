using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebApp.Models;

namespace WebApp.Services;

public interface ILibrarianBookSearchService
{
    Task<IReadOnlyList<BookCardViewModel>> FindPossibleBooksAsync(string query, string userId, CancellationToken ct = default);
}

public sealed class LibrarianBookSearchService(
    AppDbContext db,
    IEmbeddingService embeddingService,
    ILogger<LibrarianBookSearchService> logger) : ILibrarianBookSearchService
{
    private const double MaxCosineDistance = 0.25;

    public async Task<IReadOnlyList<BookCardViewModel>> FindPossibleBooksAsync(string query, string userId, CancellationToken ct = default)
    {
        try
        {
            var vector = await embeddingService.EmbedAsync(query, ct);
            var closest = await FindClosestEmbeddingsAsync(userId, vector, ct);

            if (closest.Count == 0)
                return [];

            var bookIds = closest.Select(e => e.BookId).ToList();
            return await db.Books
                .AsNoTracking()
                .Where(b => bookIds.Contains(b.Id) && b.UserId == userId)
                .Select(b => new BookCardViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    CoverUrl = b.CoverUrl,
                    NotesCount = b.Notes.Count,
                    UpdatedAt = b.UpdatedAt
                })
                .OrderByDescending(b => b.UpdatedAt)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Librarian book search failed for user {UserId}.", userId);
            return [];
        }
    }

    private async Task<List<ClosestBookEmbedding>> FindClosestEmbeddingsAsync(
        string userId,
        float[] queryEmbedding,
        CancellationToken ct)
    {
        var userParam = new NpgsqlParameter("user_id", userId);
        var vectorParam = new NpgsqlParameter("query_vector", ToVectorLiteral(queryEmbedding));
        var distanceParam = new NpgsqlParameter("max_distance", MaxCosineDistance);

        return await db.Database
            .SqlQueryRaw<ClosestBookEmbedding>(
                """
                SELECT "BookId", "Embedding" <=> @query_vector::vector AS "Distance"
                FROM book_embedding
                WHERE "UserId" = @user_id
                  AND "Embedding" <=> @query_vector::vector <= @max_distance
                ORDER BY "Embedding" <=> @query_vector::vector
                """,
                userParam,
                vectorParam,
                distanceParam)
            .ToListAsync(ct);
    }

    private static string ToVectorLiteral(IReadOnlyList<float> values) =>
        $"[{string.Join(",", values.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))}]";

    private sealed class ClosestBookEmbedding
    {
        public Guid BookId { get; set; }
        public double Distance { get; set; }
    }
}
