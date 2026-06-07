using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApp.Models;

namespace WebApp.Services;

public record KindleImportSummary(int BooksTouched, int NotesImported, int DuplicatesSkipped, int InvalidEntriesSkipped);

public interface IKindleClippingsImportService
{
    Task<KindleImportSummary> ImportAsync(string userId, Stream stream, CancellationToken ct = default);
}

internal sealed record ParsedClipping(
    string Title,
    string SourceBookTitle,
    string Author,
    string EntryType,
    string LocationText,
    string Content,
    DateTime ClippedAtUtc,
    string DedupeKey);

public class KindleClippingsImportService : IKindleClippingsImportService
{
    private static readonly Regex HeaderRegex = new(@"^(?<title>.+)\s\((?<author>.+)\)$", RegexOptions.Compiled);
    private static readonly Regex AuthorPrefixedTitleRegex = new(@"^\s*(?<authorPrefix>.+?)\s+-\s+(?<title>.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex AddedRegex = new(@"Adicionado:\s*(?<date>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<KindleClippingsImportService> _logger;

    public KindleClippingsImportService(
        AppDbContext db,
        IEmbeddingService embeddingService,
        ILogger<KindleClippingsImportService> logger)
    {
        _db = db;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<KindleImportSummary> ImportAsync(string userId, Stream stream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);

        var parsedImport = ParseEntries(text);
        var parsedEntries = parsedImport.Entries;

        if (parsedEntries.Count == 0)
        {
            return new KindleImportSummary(0, 0, 0, parsedImport.InvalidEntriesSkipped);
        }

        var normalizedBooks = parsedEntries
            .Select(x => new
            {
                x.Title,
                x.SourceBookTitle,
                x.Author,
                NormalizedTitle = NormalizeKey(x.Title),
                NormalizedSourceBookTitle = NormalizeSourceTitle(x.SourceBookTitle),
                NormalizedAuthor = NormalizeKey(x.Author)
            })
            .Distinct()
            .ToList();

        var existingBooks = await _db.Books
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var bookMap = existingBooks.ToDictionary(
            x => BuildBookLookupKey(NormalizeSourceTitle(x.SourceBookTitle), x.NormalizedAuthor),
            x => x);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        foreach (var parsedBook in normalizedBooks)
        {
            var lookupKey = BuildBookLookupKey(parsedBook.NormalizedSourceBookTitle, parsedBook.NormalizedAuthor);
            if (bookMap.ContainsKey(lookupKey))
            {
                continue;
            }

            var book = new Book
            {
                UserId = userId,
                Title = parsedBook.Title,
                SourceBookTitle = parsedBook.SourceBookTitle,
                Author = parsedBook.Author,
                NormalizedTitle = parsedBook.NormalizedTitle,
                NormalizedAuthor = parsedBook.NormalizedAuthor,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Books.Add(book);
            bookMap[lookupKey] = book;
        }

        await _db.SaveChangesAsync(ct);

        var importedBookIds = normalizedBooks
            .Select(parsedBook => bookMap[BuildBookLookupKey(parsedBook.NormalizedSourceBookTitle, parsedBook.NormalizedAuthor)].Id)
            .ToList();

        var embeddedBookIds = await _db.BookEmbeddings
            .Where(e => e.UserId == userId && importedBookIds.Contains(e.BookId))
            .Select(e => e.BookId)
            .ToListAsync(ct);

        foreach (var bookId in importedBookIds.Except(embeddedBookIds))
        {
            var book = bookMap.Values.Single(b => b.Id == bookId);
            var embedding = await _embeddingService.EmbedAsync($"{book.Title} by {book.Author}", ct);
            _db.BookEmbeddings.Add(new BookEmbedding
            {
                UserId = userId,
                BookId = book.Id,
                Title = book.Title,
                Author = book.Author,
                Embedding = new Pgvector.Vector(embedding)
            });
        }

        if (importedBookIds.Count != embeddedBookIds.Count)
            await _db.SaveChangesAsync(ct);

        var dedupeKeys = parsedEntries.Select(x => x.DedupeKey).Distinct().ToList();
        var existingNotes = await _db.BookNotes
            .Where(x => x.UserId == userId && dedupeKeys.Contains(x.DedupeKey))
            .ToDictionaryAsync(x => x.DedupeKey, x => x, ct);

        var knownDedupeKeys = existingNotes.Keys.ToHashSet();
        var touchedBooks = new HashSet<Guid>();
        var importedCount = 0;
        var duplicateCount = 0;

        foreach (var entry in parsedEntries)
        {
            var bookKey = BuildBookLookupKey(NormalizeSourceTitle(entry.SourceBookTitle), NormalizeKey(entry.Author));
            var book = bookMap[bookKey];
            touchedBooks.Add(book.Id);

            if (existingNotes.TryGetValue(entry.DedupeKey, out var existingNote))
            {
                existingNote.EntryType = entry.EntryType;
                existingNote.LocationText = entry.LocationText;
                existingNote.Content = entry.Content;
                existingNote.ClippedAtUtc = entry.ClippedAtUtc;
                existingNote.BookId = book.Id;
                existingNote.UpdatedAt = DateTime.UtcNow;
                duplicateCount++;
                continue;
            }

            if (knownDedupeKeys.Contains(entry.DedupeKey))
            {
                duplicateCount++;
                continue;
            }

            _db.BookNotes.Add(new BookNote
            {
                UserId = userId,
                BookId = book.Id,
                EntryType = entry.EntryType,
                LocationText = entry.LocationText,
                Content = entry.Content,
                ClippedAtUtc = entry.ClippedAtUtc,
                DedupeKey = entry.DedupeKey,
                UpdatedAt = DateTime.UtcNow
            });

            knownDedupeKeys.Add(entry.DedupeKey);
            importedCount++;
        }

        foreach (var book in bookMap.Values.Where(x => touchedBooks.Contains(x.Id)))
        {
            book.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        await EmbedNewNotesAsync(userId, ct);

        await transaction.CommitAsync(ct);

        return new KindleImportSummary(touchedBooks.Count, importedCount, duplicateCount, parsedImport.InvalidEntriesSkipped);
    }

    private async Task EmbedNewNotesAsync(string userId, CancellationToken ct)
    {
        var embeddedNoteIds = await _db.BookNoteEmbeddings
            .Where(e => e.UserId == userId)
            .Select(e => e.BookNoteId)
            .ToListAsync(ct);

        var embeddedSet = embeddedNoteIds.ToHashSet();

        var notesToEmbed = await _db.BookNotes
            .AsNoTracking()
            .Where(n => n.UserId == userId && !embeddedSet.Contains(n.Id))
            .ToListAsync(ct);

        foreach (var note in notesToEmbed)
        {
            try
            {
                var vector = await _embeddingService.EmbedAsync(note.Content, ct);
                _db.BookNoteEmbeddings.Add(new BookNoteEmbedding
                {
                    UserId = userId,
                    BookId = note.BookId,
                    BookNoteId = note.Id,
                    Embedding = new Pgvector.Vector(vector)
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to embed note {NoteId} for user {UserId}; skipping.", note.Id, userId);
            }
        }

        if (notesToEmbed.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    private static (List<ParsedClipping> Entries, int InvalidEntriesSkipped) ParseEntries(string text)
    {
        var entries = new List<ParsedClipping>();
        var invalidEntries = 0;

        foreach (var rawBlock in text.Split("==========", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseBlock(rawBlock, out var parsed))
            {
                invalidEntries++;
                continue;
            }

            if (parsed is null)
            {
                continue;
            }

            entries.Add(parsed);
        }

        return (entries, invalidEntries);
    }

    private static bool TryParseBlock(string block, out ParsedClipping? clipping)
    {
        clipping = null;

        var lines = block
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(x => x.Trim('\uFEFF'))
            .ToArray();

        if (lines.Length < 2)
        {
            return false;
        }

        var header = lines[0].Trim();
        var metadata = lines[1].Trim();
        var contentStartIndex = lines.Length > 2 && string.IsNullOrWhiteSpace(lines[2]) ? 3 : 2;
        var content = string.Join("\n", lines.Skip(contentStartIndex)).Trim();

        var headerMatch = HeaderRegex.Match(header);
        if (!headerMatch.Success)
        {
            return false;
        }

        var entryType = ParseEntryType(metadata);
        if (entryType == "Marker")
        {
            return true;
        }

        if (entryType is null)
        {
            return false;
        }

        var dateMatch = AddedRegex.Match(metadata);
        if (!dateMatch.Success)
        {
            return false;
        }

        if (!DateTime.TryParse(dateMatch.Groups["date"].Value.Trim(), PtBr, DateTimeStyles.AssumeLocal, out var parsedDate))
        {
            return false;
        }

        var locationText = metadata.Split('|')[0].Trim().TrimStart('-').Trim();
        var sourceBookTitle = headerMatch.Groups["title"].Value.Trim();
        var title = CleanAuthorPrefixedTitle(sourceBookTitle);
        var author = headerMatch.Groups["author"].Value.Trim();
        var clippedAtUtc = parsedDate.ToUniversalTime();
        var normalizedTitle = NormalizeKey(title);
        var normalizedSourceTitle = NormalizeSourceTitle(sourceBookTitle);
        var normalizedAuthor = NormalizeKey(author);
        var dedupeKey = ComputeDedupeKey(normalizedSourceTitle, normalizedAuthor, entryType, locationText, content, clippedAtUtc);

        clipping = new ParsedClipping(title, sourceBookTitle, author, entryType, locationText, content, clippedAtUtc, dedupeKey);
        return true;
    }

    private static string CleanAuthorPrefixedTitle(string title)
    {
        var match = AuthorPrefixedTitleRegex.Match(title);
        if (!match.Success)
            return title;

        var cleanedTitle = match.Groups["title"].Value.Trim();
        return string.IsNullOrWhiteSpace(cleanedTitle) ? title : cleanedTitle;
    }

    private static string NormalizeSourceTitle(string sourceTitle) =>
        NormalizeKey(CleanAuthorPrefixedTitle(sourceTitle));

    private static string? ParseEntryType(string metadata)
    {
        var normalized = metadata.ToLowerInvariant();

        if (normalized.Contains("seu destaque") || normalized.Contains("your highlight"))
        {
            return "Highlight";
        }

        if (normalized.Contains("sua nota") || normalized.Contains("your note"))
        {
            return "Note";
        }

        if (normalized.Contains("seu marcador") || normalized.Contains("your bookmark"))
        {
            return "Marker";
        }

        return null;
    }

    private static string ComputeDedupeKey(string normalizedTitle, string normalizedAuthor, string entryType, string locationText, string content, DateTime clippedAtUtc)
    {
        var payload = string.Join("|", normalizedTitle, normalizedAuthor, entryType, NormalizeKey(locationText), NormalizeKey(content), clippedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static string NormalizeKey(string? value)
    {
        return string.Join(' ', (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string BuildBookLookupKey(string normalizedTitle, string normalizedAuthor)
        => $"{normalizedTitle}::{normalizedAuthor}";
}
