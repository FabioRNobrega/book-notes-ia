using Microsoft.EntityFrameworkCore;

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

public class BookTitleService(AppDbContext db) : IBookTitleService
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

        await db.SaveChangesAsync(ct);

        return new BookTitleUpdateResult(BookTitleUpdateStatus.Success, book.Id, book.Title);
    }

    private static string NormalizeKey(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
