using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using WebApp.Models;

namespace WebApp.Services;

public interface IBookContextAgentTool
{
    AIFunction Create(string userId);
}

public sealed class BookContextAgentTool(
    AppDbContext db,
    IBookContextService bookContextService,
    IEmbeddingService embeddingService) : IBookContextAgentTool
{
    private const double MaxCosineDistance = 0.5;

    public AIFunction Create(string userId)
    {
        return AIFunctionFactory.Create(
            async (string bookTitle, CancellationToken ct) =>
            {
                var match = await FindByEmbeddingAsync(bookTitle, userId, ct)
                    ?? await FindByStringMatchAsync(bookTitle, userId, ct);

                if (match is null)
                    return $"Book '{bookTitle}' was not found in your library.";

                if (!string.IsNullOrWhiteSpace(match.Context))
                    return match.Context;

                return await bookContextService.GenerateAndSaveAsync(match.Id, userId, ct);
            },
            name: "GenerateBookContext",
            description: "Retrieves or generates literary context for a book in the user's reading library. " +
                         "Call this when the user asks about a specific book that appears in their library list. " +
                         "Returns a concise paragraph covering the author's background, historical setting, literary movement, and main themes.");
    }

    private async Task<Book?> FindByEmbeddingAsync(string bookTitle, string userId, CancellationToken ct)
    {
        var queryVector = new Vector(await embeddingService.EmbedAsync(bookTitle, ct));

        var closest = await db.BookEmbeddings
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Embedding.CosineDistance(queryVector))
            .Select(e => new
            {
                e.BookId,
                Distance = e.Embedding.CosineDistance(queryVector)
            })
            .FirstOrDefaultAsync(ct);

        if (closest is null || closest.Distance > MaxCosineDistance)
            return null;

        return await db.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == closest.BookId && b.UserId == userId, ct);
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

    private static string NormalizeKey(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
