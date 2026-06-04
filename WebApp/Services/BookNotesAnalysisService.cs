using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebApp.Models;

namespace WebApp.Services;

public interface IBookNotesAnalysisService
{
    Task<string> GetNotesAsync(Book book, string userId, CancellationToken ct = default);
}

public sealed class BookNotesAnalysisService(
    AppDbContext db,
    IConfiguration configuration) : IBookNotesAnalysisService
{
    private int MaxNotes => configuration.GetValue<int?>("BookNotes:MaxAnalysisNotes") ?? 50;

    public async Task<string> GetNotesAsync(Book book, string userId, CancellationToken ct = default)
    {
        var notes = await db.BookNotes
            .AsNoTracking()
            .Where(n => n.BookId == book.Id && n.UserId == userId)
            .OrderBy(n => n.ClippedAtUtc)
            .Take(MaxNotes)
            .ToListAsync(ct);

        if (notes.Count == 0)
            return $"No notes or highlights found for \"{book.Title}\" by {book.Author}.";

        var header = $"Notes for \"{book.Title}\" by {book.Author} ({notes.Count} highlights):";
        var body = string.Join(
            Environment.NewLine,
            notes.Select(n => $"<note>{n.Content}</note>"));

        return $"{header}{Environment.NewLine}{Environment.NewLine}{body}";
    }
}
