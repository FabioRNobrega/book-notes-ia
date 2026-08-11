using EbookParseService.Api.Options;
using EbookParseService.Api.Services;
using Microsoft.Extensions.Options;

namespace EbookParseService.Tests;

public sealed class EpubChapterParserTests
{
    [Fact]
    public async Task ParseAsync_UsesNavigationFragmentsAndKeepsChaptersIsolated()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        new SyntheticEpubBuilder().Build(System.IO.Path.Combine(input, "fixture.epub"));
        var parser = CreateParser(input);

        var result = await parser.ParseAsync("fixture.epub");

        Assert.Equal("Á Sample Book", result.Title);
        Assert.Equal("en-US", result.Language);
        Assert.Equal(3, result.Chapters.Count);
        Assert.Equal([1, 2, 3], result.Chapters.Select(chapter => chapter.Number));
        Assert.Contains("First synthetic paragraph — safe.", result.Chapters[0].Paragraphs);
        Assert.Contains("Second paragraph.", result.Chapters[0].Paragraphs);
        Assert.DoesNotContain(result.Chapters[0].Paragraphs, text => text.Contains("chapter two", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Chapters.SelectMany(chapter => chapter.Paragraphs), text =>
            text.Contains("PART ONE", StringComparison.Ordinal)
            || text.Contains("private script", StringComparison.Ordinal)
            || text.Contains("navigation noise", StringComparison.Ordinal)
            || text.Contains("image words", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_AcceptsExplicitSemanticChapterWithNonNumericLabel()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var navigation = """
            <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
              <body><nav epub:type="toc"><ol><li><a href="../text/book.xhtml#opening">Opening</a></li></ol></nav></body>
            </html>
            """;
        var content = """
            <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
              <body><section id="opening" epub:type="chapter"><h2>Opening</h2><p>Semantic chapter prose.</p></section></body>
            </html>
            """;
        new SyntheticEpubBuilder().WithNavigation(navigation).WithContent(content)
            .Build(System.IO.Path.Combine(input, "semantic.epub"));

        var result = await CreateParser(input).ParseAsync("semantic.epub");

        Assert.Single(result.Chapters);
        Assert.Equal("Semantic chapter prose.", Assert.Single(result.Chapters[0].Paragraphs));
    }

    [Theory]
    [InlineData("../fixture.epub")]
    [InlineData("nested/fixture.epub")]
    [InlineData("/tmp/fixture.epub")]
    [InlineData("fixture.zip")]
    [InlineData("missing.epub")]
    public async Task ParseAsync_RejectsUnsafeOrMissingInputNames(string fileName)
    {
        using var temp = new TemporaryDirectory();
        var parser = CreateParser(Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName);

        var exception = await Assert.ThrowsAsync<EpubParseException>(() => parser.ParseAsync(fileName));

        Assert.Equal(EpubParseErrorKind.InvalidRequest, exception.Kind);
        Assert.DoesNotContain(temp.Path, exception.PublicMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParseAsync_RejectsSymlinkThatResolvesOutsideInputFolder()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var outside = System.IO.Path.Combine(temp.Path, "outside.epub");
        new SyntheticEpubBuilder().Build(outside);
        File.CreateSymbolicLink(System.IO.Path.Combine(input, "linked.epub"), outside);

        var exception = await Assert.ThrowsAsync<EpubParseException>(() => CreateParser(input).ParseAsync("linked.epub"));

        Assert.Equal(EpubParseErrorKind.InvalidRequest, exception.Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ParseAsync_RejectsWrongOrReorderedMimetype(bool wrongValue)
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var builder = wrongValue
            ? new SyntheticEpubBuilder().WithWrongMimetype()
            : new SyntheticEpubBuilder().WithMimetypeAfterContainer();
        builder.Build(System.IO.Path.Combine(input, "invalid.epub"));

        var exception = await Assert.ThrowsAsync<EpubParseException>(() => CreateParser(input).ParseAsync("invalid.epub"));

        Assert.Equal(EpubParseErrorKind.InvalidEpub, exception.Kind);
    }

    [Fact]
    public async Task ParseAsync_RejectsEncryptedPublication()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        new SyntheticEpubBuilder().WithEncryption().Build(System.IO.Path.Combine(input, "protected.epub"));

        var exception = await Assert.ThrowsAsync<EpubParseException>(() => CreateParser(input).ParseAsync("protected.epub"));

        Assert.Equal(EpubParseErrorKind.UnsupportedContent, exception.Kind);
    }

    [Fact]
    public async Task ParseAsync_RejectsDuplicateChapterNumber()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var navigation = SyntheticEpubBuilder.DefaultNavigation.Replace(
            "../text/book.xhtml#two\">2</a>",
            "../text/book.xhtml#one\">1</a>",
            StringComparison.Ordinal);
        new SyntheticEpubBuilder().WithNavigation(navigation).Build(System.IO.Path.Combine(input, "duplicate.epub"));

        var exception = await Assert.ThrowsAsync<EpubParseException>(() => CreateParser(input).ParseAsync("duplicate.epub"));

        Assert.Equal(EpubParseErrorKind.UnsupportedStructure, exception.Kind);
    }

    [Fact]
    public async Task ParseAsync_EnforcesConfiguredEntryLimit()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        new SyntheticEpubBuilder().Build(System.IO.Path.Combine(input, "large.epub"));
        var parser = new EpubChapterParser(Options.Create(new EpubParserOptions
        {
            InputDirectory = input,
            OutputDirectory = System.IO.Path.Combine(temp.Path, "output"),
            MaxArchiveEntryCount = 2
        }));

        var exception = await Assert.ThrowsAsync<EpubParseException>(() => parser.ParseAsync("large.epub"));

        Assert.Equal(EpubParseErrorKind.InvalidEpub, exception.Kind);
    }

    private static EpubChapterParser CreateParser(string input) =>
        new(Options.Create(new EpubParserOptions
        {
            InputDirectory = input,
            OutputDirectory = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(input)!, "output")
        }));
}
