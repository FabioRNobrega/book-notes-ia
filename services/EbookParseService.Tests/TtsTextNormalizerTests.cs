using EbookParseService.Api.Models;
using EbookParseService.Api.Services;

namespace EbookParseService.Tests;

public sealed class TtsTextNormalizerTests
{
    private readonly TtsTextNormalizer _normalizer = new();

    [Fact]
    public void Normalize_RemovesOnlyStandaloneSceneSeparator()
    {
        var book = Book(["Before.", "  ***  ", "Keep *** inside.", "After."]);

        var result = _normalizer.Normalize(book, new TtsTextOptions("en"));

        Assert.Equal(["Before.", "Keep *** inside.", "After."], result.Chapters[0].Paragraphs);
        Assert.Equal(["Before.", "  ***  ", "Keep *** inside.", "After."], book.Chapters[0].Paragraphs);
    }

    [Theory]
    [InlineData("– Não sei – disse Case.", "Não sei, disse Case.")]
    [InlineData("— Pare! — Ele correu.", "Pare! Ele correu.")]
    [InlineData("– Talvez? – perguntou Molly.", "Talvez? perguntou Molly.")]
    [InlineData("– Espere… – disse ele.", "Espere… disse ele.")]
    public void Normalize_AppliesPortugueseDialogueRules(string input, string expected)
    {
        var result = _normalizer.Normalize(Book([input]), new TtsTextOptions("pt"));

        Assert.Equal(expected, result.Chapters[0].Paragraphs[0]);
    }

    [Fact]
    public void Normalize_PreservesEnglishDialogueDashes()
    {
        var result = _normalizer.Normalize(
            Book(["  – Hello – she said.  "]),
            new TtsTextOptions("en"));

        Assert.Equal("– Hello – she said.", result.Chapters[0].Paragraphs[0]);
        Assert.Equal("Chapter", result.ChapterLabel);
        Assert.Equal("en", result.ChapterNumberLanguage);
    }

    [Fact]
    public void Normalize_UsesPortugueseChapterLabelFromExplicitTtsLanguage()
    {
        var book = Book(["Texto."]);

        var result = _normalizer.Normalize(book, new TtsTextOptions("pt"));

        Assert.Equal("Capítulo", result.ChapterLabel);
        Assert.Equal("pt", result.ChapterNumberLanguage);
        Assert.Equal("ignored-metadata", result.Language);
        Assert.Null(book.ChapterNumberLanguage);
    }

    [Theory]
    [InlineData("Wait... there.", "Wait, there.")]
    [InlineData("Finished....", "Finished.")]
    [InlineData("One..... two...", "One, two.")]
    public void Normalize_NormalizesRepeatedPeriods(string input, string expected)
    {
        var result = _normalizer.Normalize(Book([input]), new TtsTextOptions("en"));

        Assert.Equal(expected, result.Chapters[0].Paragraphs[0]);
    }

    [Fact]
    public void Normalize_CollapsesHorizontalWhitespaceWithoutMergingParagraphs()
    {
        var result = _normalizer.Normalize(
            Book(["  First\t\t paragraph.  ", "  Second\u00a0\u00a0paragraph. "]),
            new TtsTextOptions("en"));

        Assert.Equal(["First paragraph.", "Second paragraph."], result.Chapters[0].Paragraphs);
    }

    [Fact]
    public void Normalize_RejectsUnsupportedLanguage()
    {
        var exception = Assert.Throws<EpubParseException>(() =>
            _normalizer.Normalize(Book(["Text."]), new TtsTextOptions("pt-BR")));

        Assert.Equal(EpubParseErrorKind.InvalidRequest, exception.Kind);
        Assert.Equal("TTS_LANG must be en or pt.", exception.PublicMessage);
    }

    [Fact]
    public void Normalize_RejectsChapterMadeEmptyBySceneSeparatorRemoval()
    {
        var exception = Assert.Throws<EpubParseException>(() =>
            _normalizer.Normalize(Book(["***"]), new TtsTextOptions("pt")));

        Assert.Equal(EpubParseErrorKind.InvalidRequest, exception.Kind);
        Assert.Equal("TTS normalization produced an empty chapter.", exception.PublicMessage);
    }

    private static ParsedEpubBook Book(IReadOnlyList<string> paragraphs) =>
        new("fixture.epub", "Fixture", "ignored-metadata", [new ParsedChapter(1, paragraphs)]);
}
