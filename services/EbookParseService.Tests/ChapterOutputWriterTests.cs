using System.Text;
using EbookParseService.Api.Models;
using EbookParseService.Api.Options;
using EbookParseService.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EbookParseService.Tests;

public sealed class ChapterOutputWriterTests
{
    [Fact]
    public async Task PublishAsync_WritesDeterministicUtf8ChapterSet()
    {
        using var temp = new TemporaryDirectory();
        var writer = CreateWriter(temp.Path);
        var book = Book(
            new ParsedChapter(1, ["First paragraph.", "Second — paragraph."]),
            new ParsedChapter(2, ["Another chapter."]));

        var response = await writer.PublishAsync(book);

        Assert.Equal(2, response.ChapterCount);
        Assert.EndsWith("a-sample-book", response.OutputDirectory, StringComparison.Ordinal);
        var bytes = await File.ReadAllBytesAsync(System.IO.Path.Combine(response.OutputDirectory, "chapter-001.txt"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf);
        Assert.Equal("Chapter 1.\n\nFirst paragraph.\n\nSecond — paragraph.\n", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task PublishAsync_ReplacesPriorDirectoryWithoutStaleFiles()
    {
        using var temp = new TemporaryDirectory();
        var writer = CreateWriter(temp.Path);
        var first = await writer.PublishAsync(Book(
            new ParsedChapter(1, ["Old one."]),
            new ParsedChapter(2, ["Old two."])));
        await File.WriteAllTextAsync(System.IO.Path.Combine(first.OutputDirectory, "stale.txt"), "stale");

        var second = await writer.PublishAsync(Book(new ParsedChapter(1, ["New one."])));

        Assert.Equal(["chapter-001.txt"], Directory.GetFiles(second.OutputDirectory).Select(System.IO.Path.GetFileName));
        Assert.Contains("New one.", await File.ReadAllTextAsync(System.IO.Path.Combine(second.OutputDirectory, "chapter-001.txt")));
        Assert.Empty(Directory.GetDirectories(temp.Path, ".*.staging-*"));
        Assert.Empty(Directory.GetDirectories(temp.Path, ".*.backup-*"));
    }

    [Fact]
    public async Task PublishAsync_InvalidBookDoesNotChangePriorOutput()
    {
        using var temp = new TemporaryDirectory();
        var writer = CreateWriter(temp.Path);
        var first = await writer.PublishAsync(Book(new ParsedChapter(1, ["Preserved."])));
        var before = await File.ReadAllBytesAsync(System.IO.Path.Combine(first.OutputDirectory, "chapter-001.txt"));

        await Assert.ThrowsAsync<EpubParseException>(() => writer.PublishAsync(Book(
            new ParsedChapter(1, ["Duplicate."]),
            new ParsedChapter(1, ["Duplicate again."]))));

        Assert.Equal(before, await File.ReadAllBytesAsync(System.IO.Path.Combine(first.OutputDirectory, "chapter-001.txt")));
    }

    [Fact]
    public async Task PublishAsync_ReportsFinalPublishedCharacterCount()
    {
        using var temp = new TemporaryDirectory();
        var writer = CreateWriter(temp.Path);

        var response = await writer.PublishAsync(Book(new ParsedChapter(1, ["TTS-ready prose."])));
        var content = await File.ReadAllTextAsync(
            System.IO.Path.Combine(response.OutputDirectory, response.Chapters[0].FileName));

        Assert.Equal(content.Length, response.Chapters[0].CharacterCount);
        Assert.Equal(["chapter-001.txt"], Directory.GetFiles(response.OutputDirectory).Select(System.IO.Path.GetFileName));
        Assert.Empty(Directory.GetDirectories(response.OutputDirectory));
    }

    [Fact]
    public async Task PublishAsync_RendersLocalizedChapterLabelWithDecimalNumber()
    {
        using var temp = new TemporaryDirectory();
        var writer = CreateWriter(temp.Path);
        var book = Book(new ParsedChapter(1, ["Texto pronto para narração."])) with
        {
            ChapterLabel = "Capítulo"
        };

        var response = await writer.PublishAsync(book);
        var content = await File.ReadAllTextAsync(
            System.IO.Path.Combine(response.OutputDirectory, "chapter-001.txt"));

        Assert.StartsWith("Capítulo 1.\n\n", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, "en", "Chapter", "Chapter One.")]
    [InlineData(24, "en", "Chapter", "Chapter Twenty-four.")]
    [InlineData(100, "en", "Chapter", "Chapter One hundred.")]
    [InlineData(101, "pt", "Capítulo", "Capítulo Cento e um.")]
    [InlineData(999, "pt", "Capítulo", "Capítulo Novecentos e noventa e nove.")]
    public async Task PublishAsync_RendersTtsChapterNumberInFull(
        int number,
        string language,
        string label,
        string expectedHeading)
    {
        using var temp = new TemporaryDirectory();
        var writer = CreateWriter(temp.Path);
        var book = Book(new ParsedChapter(number, ["Narration-ready prose."])) with
        {
            ChapterLabel = label,
            ChapterNumberLanguage = language
        };

        var response = await writer.PublishAsync(book);
        var expectedFileName = $"chapter-{number:000}.txt";
        var content = await File.ReadAllTextAsync(
            System.IO.Path.Combine(response.OutputDirectory, expectedFileName));

        Assert.StartsWith($"{expectedHeading}\n\n", content, StringComparison.Ordinal);
        Assert.Equal(number, response.Chapters[0].Number);
        Assert.Equal(expectedFileName, response.Chapters[0].FileName);
        Assert.Equal(content.Length, response.Chapters[0].CharacterCount);
    }

    [Fact]
    public async Task PublishAsync_FallsBackToInvariantDigitsAboveWordRange()
    {
        using var temp = new TemporaryDirectory();
        var writer = CreateWriter(temp.Path);
        var book = Book(new ParsedChapter(1000, ["Future chapter."])) with
        {
            ChapterNumberLanguage = "en"
        };

        var response = await writer.PublishAsync(book);
        var content = await File.ReadAllTextAsync(
            System.IO.Path.Combine(response.OutputDirectory, "chapter-1000.txt"));

        Assert.StartsWith("Chapter 1000.\n\n", content, StringComparison.Ordinal);
        Assert.Equal("chapter-1000.txt", response.Chapters[0].FileName);
    }

    private static ChapterOutputWriter CreateWriter(string output) =>
        new(Options.Create(new EpubParserOptions
        {
            InputDirectory = System.IO.Path.Combine(output, "input"),
            OutputDirectory = output
        }), NullLogger<ChapterOutputWriter>.Instance, new NumberToWordsConverter());

    private static ParsedEpubBook Book(params ParsedChapter[] chapters) =>
        new("fixture.epub", "Á Sample Book", "en-US", chapters);
}
