using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using WebApp.Models;

namespace WebApp.Services;

public interface IBookTitleService
{
    Task<BookTitleUpdateResult> UpdateTitleAsync(Guid bookId, string userId, string? title, CancellationToken ct = default);
}

public enum BookTitleUpdateStatus
{
    Success,
    ValidationError,
    NotFound
}

public sealed record BookTitleUpdateResult(
    BookTitleUpdateStatus Status,
    Guid BookId,
    string Title,
    string? ErrorMessage = null);

public class BookTitleService(
    AppDbContext db,
    IEmbeddingService embeddingService,
    ILogger<BookTitleService> logger) : IBookTitleService
{
    public async Task<BookTitleUpdateResult> UpdateTitleAsync(Guid bookId, string userId, string? title, CancellationToken ct = default)
    {
        var book = await db.Books
            .FirstOrDefaultAsync(x => x.Id == bookId && x.UserId == userId, ct);

        if (book is null)
            return new BookTitleUpdateResult(BookTitleUpdateStatus.NotFound, bookId, string.Empty);

        var trimmedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            return new BookTitleUpdateResult(
                BookTitleUpdateStatus.ValidationError,
                book.Id,
                book.Title,
                "Title can't be empty.");
        }

        book.Title = trimmedTitle;
        book.NormalizedTitle = NormalizeKey(trimmedTitle);
        book.UpdatedAt = DateTime.UtcNow;

        await TrySyncBookEmbeddingAsync(book, userId, trimmedTitle, ct);

        await db.SaveChangesAsync(ct);

        return new BookTitleUpdateResult(BookTitleUpdateStatus.Success, book.Id, book.Title);
    }

    private async Task TrySyncBookEmbeddingAsync(Book book, string userId, string trimmedTitle, CancellationToken ct)
    {
        try
        {
            var vector = await embeddingService.EmbedAsync($"{trimmedTitle} by {book.Author}", ct);
            var embedding = await db.BookEmbeddings
                .FirstOrDefaultAsync(x => x.BookId == book.Id && x.UserId == userId, ct);

            if (embedding is null)
            {
                db.BookEmbeddings.Add(new BookEmbedding
                {
                    UserId = userId,
                    BookId = book.Id,
                    Title = trimmedTitle,
                    Author = book.Author,
                    Embedding = new Vector(vector)
                });
                return;
            }

            embedding.Title = trimmedTitle;
            embedding.Embedding = new Vector(vector);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update embedding for book {BookId} after title change.", book.Id);
        }
    }

    private static string NormalizeKey(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
