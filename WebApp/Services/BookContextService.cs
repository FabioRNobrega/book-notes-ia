using Microsoft.EntityFrameworkCore;
using WebApp.Models;

namespace WebApp.Services;

public class BookContextService(AppDbContext db, IOllamaService ollamaService) : IBookContextService
{
    public async Task<string?> GetContextAsync(Guid bookId, string userId)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.Id == bookId && b.UserId == userId);

        return book?.Context;
    }

    public async Task<string> GenerateAndSaveAsync(Guid bookId, string userId, CancellationToken ct = default)
    {
        var generated = await GenerateToolResponseAsync(bookId, userId, context: null, ct);
        return generated.GeneratedContext;
    }

    public async Task<GenerateBookContextToolResult> GenerateToolResponseAsync(Guid bookId, string userId, string? context, CancellationToken ct = default)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(b => b.Id == bookId && b.UserId == userId, ct)
            ?? throw new KeyNotFoundException($"Book {bookId} not found for user.");

        var generatedContext = await GenerateContextAsync(book, userId, ct);

        book.Context = generatedContext;
        book.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new GenerateBookContextToolResult(
            book.Id,
            book.Title,
            book.Author,
            generatedContext,
            AppendContext(context, generatedContext, book));
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

    private async Task<string> GenerateContextAsync(Book book, string userId, CancellationToken ct)
    {
        var profile = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var language = profile?.PreferredLanguage ?? "English";

        var prompt = $"""
            You are a literary assistant. Write a concise contextual paragraph for the following book.
            Author: {book.Author}
            Book: {book.Title}

            Include: the author's life period and nationality, the historical/political context when written,
            the literary movement, the main themes, and any other relevant cultural context.
            Respond in {language}. Keep it under 120 words. Plain text only, no markdown, no lists.
            """;

        return await ollamaService.CompleteAsync(prompt, ct);
    }

    private static string AppendContext(string? existingContext, string generatedContext, Book book)
    {
        var trimmedExisting = existingContext?.Trim();
        var trimmedGenerated = generatedContext.Trim();

        var addition = $"""
            [GenerateBookContext]
            Book: {book.Title}
            Author: {book.Author}
            Summary: {trimmedGenerated}
            """.Trim();

        return string.IsNullOrWhiteSpace(trimmedExisting)
            ? addition
            : $"{trimmedExisting}{Environment.NewLine}{Environment.NewLine}{addition}";
    }
}
