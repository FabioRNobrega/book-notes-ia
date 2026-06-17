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

                var context = !string.IsNullOrWhiteSpace(match.Context)
                    ? match.Context
                    : await bookContextService.GenerateAndSaveAsync(match.Id, userId, ct);

                return $"<book-context>\n{context}\n</book-context>";
            },
            name: "GenerateBookContext",
            description: "Retrieves or generates literary context for a book in the user's reading library. " +
                         "Call this when the user asks about a specific book that appears in their library list. " +
                         "Returns a <book-context> block covering the author's background, historical setting, literary movement, and main themes. " +
                         "The content may be in any language — translate it into the reader's preferred language before using it in your response.");
    }
}
