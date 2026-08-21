using System.Globalization;
using System.Text;
using EbookParseService.Api.Models;
using EbookParseService.Api.Options;
using Microsoft.Extensions.Options;

namespace EbookParseService.Api.Services;

public sealed class ChapterOutputWriter(
    IOptions<EpubParserOptions> options,
    ILogger<ChapterOutputWriter> logger) : IChapterOutputWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private readonly EpubParserOptions _options = options.Value;

    public async Task<ParseEpubResponse> PublishAsync(
        ParsedEpubBook book,
        CancellationToken cancellationToken = default)
    {
        ValidateBook(book);
        var outputRoot = Path.GetFullPath(_options.OutputDirectory);
        Directory.CreateDirectory(outputRoot);
        var slug = CreateSlug(book.Title);
        var destination = EnsureChild(outputRoot, Path.Combine(outputRoot, slug));
        var staging = EnsureChild(outputRoot, Path.Combine(outputRoot, $".{slug}.staging-{Guid.NewGuid():N}"));
        var backup = EnsureChild(outputRoot, Path.Combine(outputRoot, $".{slug}.backup-{Guid.NewGuid():N}"));
        var summaries = new List<ParsedChapterSummary>(book.Chapters.Count);
        var oldMoved = false;
        var newPublished = false;

        try
        {
            Directory.CreateDirectory(staging);
            foreach (var chapter in book.Chapters.OrderBy(chapter => chapter.Number))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = $"chapter-{chapter.Number:000}.txt";
                var content = Render(chapter, book.ChapterLabel);
                await File.WriteAllTextAsync(Path.Combine(staging, fileName), content, Utf8WithoutBom, cancellationToken);
                summaries.Add(new ParsedChapterSummary(chapter.Number, fileName, content.Length));
            }

            var stagedFiles = Directory.GetFiles(staging, "chapter-*.txt", SearchOption.TopDirectoryOnly);
            if (stagedFiles.Length != book.Chapters.Count)
            {
                throw new IOException("The staged chapter set is incomplete.");
            }

            if (Directory.Exists(destination))
            {
                Directory.Move(destination, backup);
                oldMoved = true;
            }

            Directory.Move(staging, destination);
            newPublished = true;

            if (oldMoved)
            {
                try
                {
                    Directory.Delete(backup, recursive: true);
                }
                catch (IOException exception)
                {
                    logger.LogWarning(exception, "Published EPUB output but could not remove its private backup directory.");
                }
            }

            logger.LogInformation(
                "Published {ChapterCount} chapters for EPUB title {BookTitle}.",
                book.Chapters.Count,
                book.Title);
            return new ParseEpubResponse(
                book.SourceFileName,
                book.Title,
                book.Language,
                destination,
                summaries.Count,
                summaries);
        }
        catch (OperationCanceledException)
        {
            RollBack(destination, staging, backup, oldMoved, newPublished);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RollBack(destination, staging, backup, oldMoved, newPublished);
            throw new EpubParseException(
                EpubParseErrorKind.OutputConflict,
                "The chapter output could not be published; the previous output was preserved.",
                exception);
        }
        finally
        {
            DeleteIfPresent(staging);
        }
    }

    private static void ValidateBook(ParsedEpubBook book)
    {
        if (string.IsNullOrWhiteSpace(book.Title)
            || string.IsNullOrWhiteSpace(book.ChapterLabel)
            || book.Chapters.Count == 0
            || book.Chapters.Any(chapter => chapter.Number < 1 || chapter.Paragraphs.Count == 0)
            || book.Chapters.Select(chapter => chapter.Number).Distinct().Count() != book.Chapters.Count)
        {
            throw new EpubParseException(
                EpubParseErrorKind.OutputConflict,
                "The parsed chapter set is not valid for publication.");
        }
    }

    private static string Render(ParsedChapter chapter, string chapterLabel)
    {
        var paragraphs = chapter.Paragraphs
            .Select(paragraph => paragraph.Trim())
            .Where(paragraph => paragraph.Length > 0)
            .ToList();
        if (paragraphs.Count == 0)
        {
            throw new EpubParseException(
                EpubParseErrorKind.OutputConflict,
                "The parsed chapter set contains an empty chapter.");
        }

        return $"{chapterLabel} {chapter.Number}.\n\n{string.Join("\n\n", paragraphs)}\n";
    }

    private static string CreateSlug(string title)
    {
        var decomposed = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var pendingSeparator = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character <= 127 && char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.Length == 0 ? "book" : builder.ToString();
    }

    private static string EnsureChild(string root, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var expectedPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new EpubParseException(
                EpubParseErrorKind.OutputConflict,
                "The output directory configuration is unsafe.");
        }

        return fullPath;
    }

    private static void RollBack(
        string destination,
        string staging,
        string backup,
        bool oldMoved,
        bool newPublished)
    {
        if (newPublished && Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        if (oldMoved && Directory.Exists(backup) && !Directory.Exists(destination))
        {
            Directory.Move(backup, destination);
        }

        DeleteIfPresent(staging);
    }

    private static void DeleteIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup; private data remains ignored.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup; private data remains ignored.
            }
        }
    }
}
