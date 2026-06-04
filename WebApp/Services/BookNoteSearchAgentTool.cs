using Microsoft.Extensions.AI;

namespace WebApp.Services;

public interface IBookNoteSearchAgentTool
{
    AIFunction Create(string userId);
}

public sealed class BookNoteSearchAgentTool(
    IBookLookupService bookLookupService,
    IBookNoteSearchService bookNoteSearchService) : IBookNoteSearchAgentTool
{
    public AIFunction Create(string userId)
    {
        return AIFunctionFactory.Create(
            async (string bookTitle, string searchQuery, CancellationToken ct) =>
            {
                var book = await bookLookupService.FindAsync(bookTitle, userId, ct);

                if (book is null)
                    return $"Book '{bookTitle}' was not found in your library.";

                var notes = await bookNoteSearchService.SearchAsync(book, searchQuery, userId, ct);

                if (notes.Count == 0)
                    return $"No relevant notes found for \"{book.Title}\" matching \"{searchQuery}\".";

                return string.Join(
                    Environment.NewLine,
                    notes.Select(n => $"<note loc=\"{n.LocationText}\">{n.Content}</note>"));
            },
            name: "GetRelevantBookNotes",
            description: "Searches the user's personal highlights and annotations for a specific book using semantic similarity. " +
                         "Call this when the user asks a focused question about a particular topic, theme, or idea within a book's notes — " +
                         "pass the user's question or topic as searchQuery. " +
                         "Returns the most relevant highlights as <note loc=\"...\">...</note> blocks. " +
                         "Use GetBookNotesWithAnalysis instead when the user wants a general thematic overview of all their notes.");
    }
}
