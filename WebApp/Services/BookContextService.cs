using Microsoft.EntityFrameworkCore;
using WebApp.Models;

namespace WebApp.Services;

public class BookContextService(
    AppDbContext db,
    IChatCompletionService chatCompletionService,
    IOpenLibraryService openLibraryService) : IBookContextService
{
    public async Task<string?> GetContextAsync(Guid bookId, string userId)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.Id == bookId && b.UserId == userId);

        return book?.Context;
    }

    public async Task<string> GenerateAndSaveAsync(Guid bookId, string userId, string agentKey, CancellationToken ct = default)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.Id == bookId && b.UserId == userId, ct)
            ?? throw new KeyNotFoundException($"Book {bookId} not found for user.");

        if (string.IsNullOrWhiteSpace(book.Synopsis))
        {
            var synopsis = await openLibraryService.GetSynopsisAsync(book.Title, book.Author, ct);
            if (!string.IsNullOrWhiteSpace(synopsis))
                book.Synopsis = synopsis;
        }

        var generatedContext = await GenerateContextAsync(book, userId, agentKey, ct);

        book.Context = generatedContext;
        book.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return generatedContext;
    }

    public async Task<string> SaveManualAsync(Guid bookId, string userId, string context)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.Id == bookId && b.UserId == userId)
            ?? throw new KeyNotFoundException($"Book {bookId} not found for user.");

        book.Context = context;
        book.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return context;
    }

    public async Task ClearAsync(Guid bookId, string userId)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.Id == bookId && b.UserId == userId)
            ?? throw new KeyNotFoundException($"Book {bookId} not found for user.");

        book.Context = null;
        book.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<string> GenerateContextAsync(Book book, string userId, string agentKey, CancellationToken ct)
    {
        var profile = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var language = profile?.PreferredLanguage ?? "English";
        var synopsisSection = string.IsNullOrWhiteSpace(book.Synopsis)
            ? string.Empty
            : $"""

            Synopsis from Open Library for factual grounding:
            {book.Synopsis}
            """;

        var prompt = $"""
            You are a literary assistant. Write a concise contextual paragraph for the following book.
            Author: {book.Author}
            Book: {book.Title}
            {synopsisSection}

            Include: the author's life period and nationality, the historical/political context when written,
            the literary movement, the main themes, and any other relevant cultural context.
            Respond in {language}. Keep it under 120 words. Plain text only, no markdown, no lists.
            """;

        return await chatCompletionService.CompleteAsync(prompt, agentKey, ct);
    }
}
