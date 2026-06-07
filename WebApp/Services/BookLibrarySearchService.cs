using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebApp.Models;

namespace WebApp.Services;

public interface IBookLibrarySearchService
{
    Task<LibrarySearchResult> SearchSqlAsync(string? query, string userId, CancellationToken ct = default);
}

public record LibrarySearchResult(
    IReadOnlyList<BookCardViewModel> Books,
    bool NoExactSqlMatch
);

public sealed class BookLibrarySearchService(AppDbContext db) : IBookLibrarySearchService
{
    private const float FuzzyThreshold = 0.3f;

    public async Task<LibrarySearchResult> SearchSqlAsync(string? query, string userId, CancellationToken ct = default)
    {
        var trimmed = query?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            var all = await db.Books
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .Select(b => new BookCardViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    CoverUrl = b.CoverUrl,
                    NotesCount = b.Notes.Count,
                    UpdatedAt = b.UpdatedAt
                })
                .OrderByDescending(b => b.UpdatedAt)
                .ToListAsync(ct);

            return new LibrarySearchResult(all, NoExactSqlMatch: false);
        }

        var lower = trimmed.ToLowerInvariant();

        var exact = await db.Books
            .AsNoTracking()
            .Where(b => b.UserId == userId &&
                        (b.Title.ToLower().Contains(lower) ||
                         b.Author.ToLower().Contains(lower)))
            .Select(b => new BookCardViewModel
            {
                Id = b.Id,
                Title = b.Title,
                Author = b.Author,
                CoverUrl = b.CoverUrl,
                NotesCount = b.Notes.Count,
                UpdatedAt = b.UpdatedAt
            })
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync(ct);

        if (exact.Count > 0)
            return new LibrarySearchResult(exact, NoExactSqlMatch: false);

        if (!db.Database.IsRelational())
            return new LibrarySearchResult([], NoExactSqlMatch: true);

        var fuzzy = await FuzzySearchAsync(lower, userId, ct);
        return new LibrarySearchResult(fuzzy, NoExactSqlMatch: fuzzy.Count == 0);
    }

    private async Task<List<BookCardViewModel>> FuzzySearchAsync(string lowerQuery, string userId, CancellationToken ct)
    {
        var userParam = new NpgsqlParameter("user_id", userId);
        var queryParam = new NpgsqlParameter("query", lowerQuery);
        var thresholdParam = new NpgsqlParameter("threshold", FuzzyThreshold);

        var matches = await db.Database
            .SqlQueryRaw<FuzzyBookRow>(
                """
                SELECT b."Id",
                       b."Title",
                       b."Author",
                       b."CoverUrl",
                       b."UpdatedAt",
                       (SELECT count(*)::int FROM book_note AS n WHERE n."BookId" = b."Id") AS "NotesCount"
                FROM book AS b
                WHERE b."UserId" = @user_id
                  AND (word_similarity(@query, lower(b."Title")) >= @threshold
                       OR word_similarity(@query, lower(b."Author")) >= @threshold)
                ORDER BY GREATEST(word_similarity(@query, lower(b."Title")),
                                  word_similarity(@query, lower(b."Author"))) DESC,
                         b."UpdatedAt" DESC
                LIMIT 20
                """,
                userParam, queryParam, thresholdParam)
            .ToListAsync(ct);

        return matches
            .Select(r => new BookCardViewModel
            {
                Id = r.Id,
                Title = r.Title,
                Author = r.Author,
                CoverUrl = r.CoverUrl,
                NotesCount = r.NotesCount,
                UpdatedAt = r.UpdatedAt
            })
            .ToList();
    }

    private sealed class FuzzyBookRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = default!;
        public string Author { get; set; } = default!;
        public string? CoverUrl { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int NotesCount { get; set; }
    }
}
