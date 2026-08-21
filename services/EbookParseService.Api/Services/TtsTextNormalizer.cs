using System.Text.RegularExpressions;
using EbookParseService.Api.Models;

namespace EbookParseService.Api.Services;

public sealed class TtsTextNormalizer : ITtsTextNormalizer
{
    private static readonly Regex HorizontalWhitespace = new(
        @"[\p{Zs}\t\f\v]+",
        RegexOptions.CultureInvariant);

    private static readonly Regex RepeatedPeriods = new(
        @"\.{3,}",
        RegexOptions.CultureInvariant);

    private static readonly Regex LeadingDialogueDash = new(
        @"^[–—]\s*",
        RegexOptions.CultureInvariant);

    private static readonly Regex InlineDialogueDash = new(
        @"\s+[–—]\s+",
        RegexOptions.CultureInvariant);

    public ParsedEpubBook Normalize(ParsedEpubBook book, TtsTextOptions options)
    {
        if (!TtsTextOptions.IsSupported(options.Language))
        {
            throw new EpubParseException(
                EpubParseErrorKind.InvalidRequest,
                "TTS_LANG must be en or pt.");
        }

        var chapters = book.Chapters
            .Select(chapter => NormalizeChapter(chapter, options.Language))
            .ToArray();

        return book with
        {
            Chapters = chapters,
            ChapterLabel = options.Language == "pt" ? "Capítulo" : "Chapter",
            ChapterNumberLanguage = options.Language
        };
    }

    private static ParsedChapter NormalizeChapter(ParsedChapter chapter, string language)
    {
        var paragraphs = chapter.Paragraphs
            .Select(paragraph => paragraph.Trim())
            .Where(paragraph => paragraph != "***")
            .Select(paragraph => NormalizeParagraph(paragraph, language))
            .Where(paragraph => paragraph.Length > 0)
            .ToArray();

        if (paragraphs.Length == 0)
        {
            throw new EpubParseException(
                EpubParseErrorKind.InvalidRequest,
                "TTS normalization produced an empty chapter.");
        }

        return chapter with { Paragraphs = paragraphs };
    }

    private static string NormalizeParagraph(string paragraph, string language)
    {
        var normalized = HorizontalWhitespace.Replace(paragraph, " ").Trim();
        normalized = RepeatedPeriods.Replace(normalized, match =>
            HasFollowingText(normalized, match.Index + match.Length) ? "," : ".");

        if (language == "pt")
        {
            normalized = LeadingDialogueDash.Replace(normalized, string.Empty);
            normalized = InlineDialogueDash.Replace(normalized, match =>
                HasTerminalPunctuation(normalized, match.Index) ? " " : ", ");
        }

        return HorizontalWhitespace.Replace(normalized, " ").Trim();
    }

    private static bool HasFollowingText(string value, int startIndex)
    {
        for (var index = startIndex; index < value.Length; index++)
        {
            if (!char.IsWhiteSpace(value[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTerminalPunctuation(string value, int endIndex)
    {
        for (var index = endIndex - 1; index >= 0; index--)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                continue;
            }

            return value[index] is '.' or '!' or '?' or '…';
        }

        return false;
    }
}
