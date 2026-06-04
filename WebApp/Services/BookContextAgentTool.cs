using Microsoft.Extensions.AI;

namespace WebApp.Services;

public interface IBookContextAgentTool
{
    AIFunction Create(string userId);
}

public sealed class BookContextAgentTool(
    IBookContextService bookContextService,
    IBookLookupService bookLookupService) : IBookContextAgentTool
{
    public AIFunction Create(string userId)
    {
        return AIFunctionFactory.Create(
            async (string bookTitle, CancellationToken ct) =>
            {
                var match = await bookLookupService.FindAsync(bookTitle, userId, ct);

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
}
