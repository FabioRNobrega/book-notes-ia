using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace WebApp.Services;

public interface IBookContextAgentTool
{
    AIFunction Create(string userId);
}

public sealed class BookContextAgentTool(AppDbContext db, IBookContextService bookContextService) : IBookContextAgentTool
{
    public AIFunction Create(string userId)
    {
        return AIFunctionFactory.Create(
            async (string bookTitle, CancellationToken ct) =>
            {
                var searchTitle = new string(
                    bookTitle.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

                var candidates = await db.Books
                    .AsNoTracking()
                    .Where(b => b.UserId == userId)
                    .OrderByDescending(b => b.UpdatedAt)
                    .Take(25)
                    .Select(b => new { b.Id, b.NormalizedTitle, b.Context })
                    .ToListAsync(ct);

                var match = candidates.FirstOrDefault(b =>
                    b.NormalizedTitle.Contains(searchTitle, StringComparison.Ordinal) ||
                    searchTitle.Contains(b.NormalizedTitle, StringComparison.Ordinal));

                if (match is null)
                    return $"No book matching '{bookTitle}' was found in your library.";

                if (!string.IsNullOrWhiteSpace(match.Context))
                    return match.Context;

                return await bookContextService.GenerateAndSaveAsync(match.Id, userId, ct);
            },
            name: "GenerateBookContext",
            description: "Retrieves or generates literary context for a book in the user's reading library. " +
                         "Call this when the user asks about a specific book that appears in their library list. " +
                         "Returns a concise paragraph covering the author's background, historical setting, literary movement, and main themes.");
    }
}
