using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
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

    private sealed record ClosestBookEmbedding(Guid BookId, double Distance);

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
        var queryEmbedding = await embeddingService.EmbedAsync(bookTitle, ct);

        ClosestBookEmbedding? closest;
        try
        {
            closest = await FindClosestEmbeddingAsync(userId, queryEmbedding, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }

        if (closest is null)
            return null;

        if (closest.Distance > MaxCosineDistance)
            return null;

        return await db.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == closest.BookId && b.UserId == userId, ct);
    }

    private async Task<ClosestBookEmbedding?> FindClosestEmbeddingAsync(
        string userId,
        IReadOnlyList<float> queryEmbedding,
        CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
            await db.Database.OpenConnectionAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT "BookId", "Embedding" <=> @query_vector::vector AS "Distance"
                FROM book_embedding
                WHERE "UserId" = @user_id
                ORDER BY "Embedding" <=> @query_vector::vector
                LIMIT 1
                """;

            var userParameter = command.CreateParameter();
            userParameter.ParameterName = "user_id";
            userParameter.Value = userId;
            command.Parameters.Add(userParameter);

            var vectorParameter = command.CreateParameter();
            vectorParameter.ParameterName = "query_vector";
            vectorParameter.Value = ToVectorLiteral(queryEmbedding);
            command.Parameters.Add(vectorParameter);

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            return new ClosestBookEmbedding(reader.GetGuid(0), reader.GetDouble(1));
        }
        finally
        {
            if (shouldCloseConnection)
                await db.Database.CloseConnectionAsync();
        }
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
}
