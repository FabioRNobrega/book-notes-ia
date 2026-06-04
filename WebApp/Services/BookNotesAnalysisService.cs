using System.Text;
using Microsoft.EntityFrameworkCore;
using WebApp.Models;

namespace WebApp.Services;

public interface IBookNotesAnalysisService
{
    Task<string> GetNotesWithAnalysisAsync(Book book, string userId, CancellationToken ct = default);
}

public sealed class BookNotesAnalysisService(
    AppDbContext db,
    IOllamaService ollamaService) : IBookNotesAnalysisService
{
    public async Task<string> GetNotesWithAnalysisAsync(Book book, string userId, CancellationToken ct = default)
    {
        var notes = await db.BookNotes
            .AsNoTracking()
            .Where(n => n.BookId == book.Id && n.UserId == userId)
            .OrderBy(n => n.ClippedAtUtc)
            .ToListAsync(ct);

        if (notes.Count == 0)
            return $"No notes or highlights found for \"{book.Title}\" by {book.Author}.";

        var formattedNotes = string.Join(
            Environment.NewLine,
            notes.Select(note => $"<note>{note.Content}</note>"));

        var profile = await db.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var language = string.IsNullOrWhiteSpace(profile?.PreferredLanguage)
            ? "English"
            : profile.PreferredLanguage;

        var prompt = $"""
            You are a literary assistant analyzing a reader's personal Kindle notes.
            Respond in {language}. Plain text only, no markdown, no lists. Keep the analysis under 150 words.

            Book: {book.Title}
            Author: {book.Author}
            Book Context:
            {book.Context ?? "Not available."}

            Notes:
            {formattedNotes}

            Answer these questions:
            What is the main relationship between these notes?
            What is the overarching theme of these notes, and how does it connect to the book's literary context?
            """;

        var analysis = await ollamaService.CompleteAsync(prompt, ct);

        var result = new StringBuilder();
        result.AppendLine($"Thematic analysis for \"{book.Title}\" by {book.Author} based on {notes.Count} personal notes:");
        result.AppendLine();
        result.Append(analysis);

        return result.ToString();
    }
}
