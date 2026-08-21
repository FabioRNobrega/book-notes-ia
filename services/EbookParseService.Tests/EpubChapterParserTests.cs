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

    [Fact]
    public async Task ParseAsync_UsesEpub2NcxWholeDocumentTargets()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        new SyntheticEpubBuilder().AsEpub2Ncx().Build(System.IO.Path.Combine(input, "ncx.epub"));

        var result = await CreateParser(input).ParseAsync("ncx.epub");

        Assert.Equal("NCX Sample Book", result.Title);
        Assert.Equal("en", result.Language);
        Assert.Equal([1, 2], result.Chapters.Select(chapter => chapter.Number));
        Assert.Equal(
            ["First NCX chapter paragraph.", "More chapter one."],
            result.Chapters[0].Paragraphs);
        Assert.Equal("Only chapter two NCX prose.", Assert.Single(result.Chapters[1].Paragraphs));
        Assert.DoesNotContain(
            result.Chapters.SelectMany(chapter => chapter.Paragraphs),
            paragraph => paragraph.Contains("Preface", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_UsesEpub2NcxFragmentTarget()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var ncx = SyntheticEpubBuilder.DefaultNcx.Replace(
            "src=\"text/chapter1.xhtml\"",
            "src=\"text/chapter1.xhtml#one\"",
            StringComparison.Ordinal);
        var chapter = """
            <html xmlns="http://www.w3.org/1999/xhtml">
              <body><section id="one"><h2>1</h2><p>Fragment NCX prose.</p></section></body>
            </html>
            """;
        new SyntheticEpubBuilder().AsEpub2Ncx()
            .WithResource("OPS/toc.ncx", ncx)
            .WithResource("OPS/text/chapter1.xhtml", chapter)
            .Build(System.IO.Path.Combine(input, "fragment.epub"));

        var result = await CreateParser(input).ParseAsync("fragment.epub");

        Assert.Equal("Fragment NCX prose.", Assert.Single(result.Chapters[0].Paragraphs));
    }

    [Fact]
    public async Task ParseAsync_NormalizesXhtmlNbspWithoutResolvingExternalDtd()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var chapter = SyntheticEpubBuilder.Epub2ChapterOne.Replace(
            "First NCX",
            "First&nbsp;NCX",
            StringComparison.Ordinal);
        new SyntheticEpubBuilder().AsEpub2Ncx().WithResource("OPS/text/chapter1.xhtml", chapter)
            .Build(System.IO.Path.Combine(input, "nbsp.epub"));

        var result = await CreateParser(input).ParseAsync("nbsp.epub");

        Assert.Contains("First NCX chapter paragraph.", result.Chapters[0].Paragraphs);
    }

    [Fact]
    public async Task ParseAsync_RejectsEpub2PackageWithoutSpineNcxReference()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var package = SyntheticEpubBuilder.Epub2Package.Replace(" toc=\"ncx\"", string.Empty, StringComparison.Ordinal);
        new SyntheticEpubBuilder().AsEpub2Ncx().WithPackage(package)
            .Build(System.IO.Path.Combine(input, "missing-ncx.epub"));

        var exception = await Assert.ThrowsAsync<EpubParseException>(
            () => CreateParser(input).ParseAsync("missing-ncx.epub"));

        Assert.Equal(EpubParseErrorKind.UnsupportedStructure, exception.Kind);
    }

    [Fact]
    public async Task ParseAsync_RejectsEpub2NcxWithoutNavMap()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        const string ncx = "<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\" />";
        new SyntheticEpubBuilder().AsEpub2Ncx().WithResource("OPS/toc.ncx", ncx)
            .Build(System.IO.Path.Combine(input, "no-nav-map.epub"));

        var exception = await Assert.ThrowsAsync<EpubParseException>(
            () => CreateParser(input).ParseAsync("no-nav-map.epub"));

        Assert.Equal(EpubParseErrorKind.UnsupportedStructure, exception.Kind);
    }

    [Fact]
    public async Task ParseAsync_RejectsEpub2NcxChapterWithMismatchedHeading()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var chapter = SyntheticEpubBuilder.Epub2ChapterOne.Replace("<h2>1</h2>", "<h2>9</h2>", StringComparison.Ordinal);
        new SyntheticEpubBuilder().AsEpub2Ncx().WithResource("OPS/text/chapter1.xhtml", chapter)
            .Build(System.IO.Path.Combine(input, "mismatch.epub"));

        var exception = await Assert.ThrowsAsync<EpubParseException>(
            () => CreateParser(input).ParseAsync("mismatch.epub"));

        Assert.Equal(EpubParseErrorKind.UnsupportedStructure, exception.Kind);
    }

    [Fact]
    public async Task ParseAsync_RejectsEpub2NcxUndeclaredChapterTarget()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var ncx = SyntheticEpubBuilder.DefaultNcx.Replace(
            "src=\"text/chapter1.xhtml\"",
            "src=\"text/missing.xhtml\"",
            StringComparison.Ordinal);
        new SyntheticEpubBuilder().AsEpub2Ncx().WithResource("OPS/toc.ncx", ncx)
            .Build(System.IO.Path.Combine(input, "undeclared.epub"));

        var exception = await Assert.ThrowsAsync<EpubParseException>(
            () => CreateParser(input).ParseAsync("undeclared.epub"));

        Assert.Equal(EpubParseErrorKind.UnsupportedStructure, exception.Kind);
    }

    [Fact]
    public async Task ParseAsync_RejectsDuplicateEpub2NcxChapterTarget()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var ncx = SyntheticEpubBuilder.DefaultNcx
            .Replace("<text>Chapter 2</text>", "<text>Chapter 1</text>", StringComparison.Ordinal)
            .Replace("src=\"text/chapter2.xhtml\"", "src=\"text/chapter1.xhtml\"", StringComparison.Ordinal);
        new SyntheticEpubBuilder().AsEpub2Ncx().WithResource("OPS/toc.ncx", ncx)
            .Build(System.IO.Path.Combine(input, "duplicate-target.epub"));

        var exception = await Assert.ThrowsAsync<EpubParseException>(
            () => CreateParser(input).ParseAsync("duplicate-target.epub"));

        Assert.Equal(EpubParseErrorKind.UnsupportedStructure, exception.Kind);
        Assert.Contains("duplicate chapter target", exception.PublicMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseAsync_InfersLanguageForNumericEpub2NcxChapters()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        BuildNumericNcxWithoutPackageLanguage(input, "numeric.epub", "pt-br", "pt-br");

        var result = await CreateParser(input).ParseAsync("numeric.epub");

        Assert.Equal("pt-br", result.Language);
        Assert.Equal([1, 2], result.Chapters.Select(chapter => chapter.Number));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("pt-br", null)]
    [InlineData("pt-br", "en")]
    public async Task ParseAsync_RejectsIncompleteOrInconsistentInferredNcxLanguage(
        string? chapterOneLanguage,
        string? chapterTwoLanguage)
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        BuildNumericNcxWithoutPackageLanguage(
            input,
            "invalid-language.epub",
            chapterOneLanguage,
            chapterTwoLanguage);

        var exception = await Assert.ThrowsAsync<EpubParseException>(
            () => CreateParser(input).ParseAsync("invalid-language.epub"));

        Assert.Equal(EpubParseErrorKind.InvalidEpub, exception.Kind);
        Assert.Contains("language", exception.PublicMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseAsync_RejectsEpub3WithoutPackageLanguage()
    {
        using var temp = new TemporaryDirectory();
        var input = Directory.CreateDirectory(System.IO.Path.Combine(temp.Path, "input")).FullName;
        var package = RemovePackageLanguage(SyntheticEpubBuilder.DefaultPackage);
        new SyntheticEpubBuilder().WithPackage(package).Build(System.IO.Path.Combine(input, "epub3-no-language.epub"));

        var exception = await Assert.ThrowsAsync<EpubParseException>(
            () => CreateParser(input).ParseAsync("epub3-no-language.epub"));

        Assert.Equal(EpubParseErrorKind.InvalidEpub, exception.Kind);
        Assert.Contains("language", exception.PublicMessage, StringComparison.OrdinalIgnoreCase);
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

    private static void BuildNumericNcxWithoutPackageLanguage(
        string input,
        string fileName,
        string? chapterOneLanguage,
        string? chapterTwoLanguage)
    {
        var package = RemovePackageLanguage(SyntheticEpubBuilder.Epub2Package);
        var ncx = SyntheticEpubBuilder.DefaultNcx
            .Replace("<text>Chapter 1</text>", "<text>1</text>", StringComparison.Ordinal)
            .Replace("<text>Chapter 2</text>", "<text>2</text>", StringComparison.Ordinal);
        var chapterOne = AddHeadingLanguage(SyntheticEpubBuilder.Epub2ChapterOne, "1", chapterOneLanguage);
        var chapterTwo = AddHeadingLanguage(SyntheticEpubBuilder.Epub2ChapterTwo, "2", chapterTwoLanguage);
        new SyntheticEpubBuilder().AsEpub2Ncx()
            .WithPackage(package)
            .WithResource("OPS/toc.ncx", ncx)
            .WithResource("OPS/text/chapter1.xhtml", chapterOne)
            .WithResource("OPS/text/chapter2.xhtml", chapterTwo)
            .Build(System.IO.Path.Combine(input, fileName));
    }

    private static string RemovePackageLanguage(string package) =>
        package.Replace("<dc:language>en-US</dc:language>", string.Empty, StringComparison.Ordinal)
            .Replace("<dc:language>en</dc:language>", string.Empty, StringComparison.Ordinal);

    private static string AddHeadingLanguage(string chapter, string number, string? language) =>
        language is null
            ? chapter
            : chapter.Replace(
                $"<h2>{number}</h2>",
                $"<h2 xml:lang=\"{language}\">{number}</h2>",
                StringComparison.Ordinal);
}
