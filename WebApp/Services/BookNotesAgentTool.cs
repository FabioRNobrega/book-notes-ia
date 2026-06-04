using Microsoft.Extensions.AI;

namespace WebApp.Services;

public interface IBookNotesAgentTool
{
    AIFunction Create(string userId);
}

public sealed class BookNotesAgentTool(
    IBookLookupService bookLookupService,
    IBookNotesAnalysisService bookNotesAnalysisService) : IBookNotesAgentTool
{
    public AIFunction Create(string userId)
    {
        return AIFunctionFactory.Create(
            async (string bookTitle, CancellationToken ct) =>
            {
                var match = await bookLookupService.FindAsync(bookTitle, userId, ct);

                if (match is null)
                    return $"Book '{bookTitle}' was not found in your library.";

                return await bookNotesAnalysisService.GetNotesWithAnalysisAsync(match, userId, ct);
            },
            name: "GetBookNotesWithAnalysis",
            description: "Retrieves the user's personal notes, highlights, or annotations for a specific book in their library " +
                         "and returns a concise thematic analysis grounded in those notes. Call this when the user asks what they noted, " +
                         "highlighted, annotated, or personally observed in a specific book. Do not list raw notes unless the user explicitly asks for the exact notes.");
    }
}
