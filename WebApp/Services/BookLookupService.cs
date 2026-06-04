using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using WebApp.Models;

namespace WebApp.Services;

public interface IBookLookupService
{
    Task<Book?> FindAsync(string bookTitle, string userId, CancellationToken ct = default);
}

public sealed class BookLookupService(
    AppDbContext db,
    IEmbeddingService embeddingService,
    ILogger<BookLookupService> logger) : IBookLookupService
{
    private const double MaxCosineDistance = 0.5;

    public async Task<Book?> FindAsync(string bookTitle, string userId, CancellationToken ct = default)
    {
        var match = await TryFindByEmbeddingAsync(bookTitle, userId, ct);
        return match ?? await FindByStringMatchAsync(bookTitle, userId, ct);
    }

    private async Task<Book?> TryFindByEmbeddingAsync(string bookTitle, string userId, CancellationToken ct)
    {
        try
        {
            var queryEmbedding = await embeddingService.EmbedAsync(bookTitle, ct);
            var closest = await FindClosestEmbeddingAsync(userId, queryEmbedding, ct);

            if (closest is null || closest.Distance > MaxCosineDistance)
                return null;

            return await db.Books
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == closest.BookId && b.UserId == userId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Vector book lookup failed for user {UserId}; falling back to string matching.", userId);
            return null;
        }
    }

    private async Task<ClosestBookEmbedding?> FindClosestEmbeddingAsync(
        string userId,
        IReadOnlyList<float> queryEmbedding,
        CancellationToken ct)
    {
        var userParameter = new NpgsqlParameter("user_id", userId);
        var vectorParameter = new NpgsqlParameter("query_vector", ToVectorLiteral(queryEmbedding));

        return await db.Database
            .SqlQueryRaw<ClosestBookEmbedding>(
                """
                SELECT "BookId", "Embedding" <=> @query_vector::vector AS "Distance"
                FROM book_embedding
                WHERE "UserId" = @user_id
                ORDER BY "Embedding" <=> @query_vector::vector
                LIMIT 1
                """,
                userParameter,
                vectorParameter)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Book?> FindByStringMatchAsync(string bookTitle, string userId, CancellationToken ct)
    {
        var searchTitles = BuildSearchTitles(bookTitle);

        var candidates = await db.Books
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync(ct);

        return candidates.FirstOrDefault(b =>
        {
            var titleAndAuthor = $"{b.NormalizedTitle}{b.NormalizedAuthor}";
            var authorAndTitle = $"{b.NormalizedAuthor}{b.NormalizedTitle}";

            return searchTitles.Any(searchTitle =>
                b.NormalizedTitle.Contains(searchTitle, StringComparison.Ordinal) ||
                searchTitle.Contains(b.NormalizedTitle, StringComparison.Ordinal) ||
                titleAndAuthor.Contains(searchTitle, StringComparison.Ordinal) ||
                searchTitle.Contains(titleAndAuthor, StringComparison.Ordinal) ||
                authorAndTitle.Contains(searchTitle, StringComparison.Ordinal) ||
                searchTitle.Contains(authorAndTitle, StringComparison.Ordinal));
        });
    }

    private static IReadOnlyList<string> BuildSearchTitles(string bookTitle)
    {
        var titles = new HashSet<string>(StringComparer.Ordinal)
        {
            NormalizeKey(bookTitle),
            NormalizeKey(bookTitle.Replace(" by ", " ", StringComparison.OrdinalIgnoreCase))
        };

        var byParts = bookTitle.Split(" by ", 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (byParts.Length > 0)
            titles.Add(NormalizeKey(byParts[0]));

        var dashParts = bookTitle.Split(" - ", 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (dashParts.Length == 2)
        {
            titles.Add(NormalizeKey(dashParts[1]));
            titles.Add(NormalizeKey($"{dashParts[1]} {dashParts[0]}"));
        }

        return titles.Where(t => t.Length > 0).ToList();
    }

    private static string ToVectorLiteral(IReadOnlyList<float> values) =>
        $"[{string.Join(",", values.Select(value => value.ToString("R", CultureInfo.InvariantCulture)))}]";

    private static string NormalizeKey(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private sealed class ClosestBookEmbedding
    {
        public Guid BookId { get; set; }
        public double Distance { get; set; }
    }
}
