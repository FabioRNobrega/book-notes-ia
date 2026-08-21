using EbookParseService.Api.Controllers;
using EbookParseService.Api.Models;
using EbookParseService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace EbookParseService.Tests;

public sealed class EpubControllerTests
{
    [Fact]
    public async Task Parse_ReturnsSafeSuccessSummaryWithoutTtsNormalizationByDefault()
    {
        var book = Book();
        var response = Response();
        var normalizer = new StubNormalizer();
        var writer = new StubWriter(response);
        var controller = new EpubController(
            new StubParser(book), normalizer, writer, NullLogger<EpubController>.Instance);

        var result = await controller.Parse(new ParseEpubRequest("fixture.epub"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<ParseEpubResponse>(ok.Value);
        Assert.Equal(response, model);
        Assert.DoesNotContain("Private prose", System.Text.Json.JsonSerializer.Serialize(model), StringComparison.Ordinal);
        Assert.Equal(0, normalizer.CallCount);
        Assert.Same(book, writer.Book);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("eng")]
    [InlineData("pt-BR")]
    public async Task Parse_RejectsMissingOrUnsupportedTtsLanguage(string? language)
    {
        var parser = new StubParser(Book());
        var normalizer = new StubNormalizer();
        var writer = new StubWriter(null!);
        var controller = new EpubController(
            parser, normalizer, writer, NullLogger<EpubController>.Instance);

        var result = await controller.Parse(
            new ParseEpubRequest("fixture.epub", Tts: true, TtsLanguage: language),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(400, objectResult.StatusCode);
        Assert.Equal("TTS=true requires TTS_LANG=en or TTS_LANG=pt.", problem.Detail);
        Assert.Equal(0, parser.CallCount);
        Assert.Equal(0, normalizer.CallCount);
        Assert.Equal(0, writer.CallCount);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("pt")]
    public async Task Parse_NormalizesWithExplicitTtsLanguage(string language)
    {
        var parsed = Book(language == "pt" ? "en" : "pt");
        var normalized = parsed with
        {
            Chapters = [new ParsedChapter(1, ["Normalized prose."])]
        };
        var normalizer = new StubNormalizer(normalized);
        var writer = new StubWriter(Response());
        var controller = new EpubController(
            new StubParser(parsed), normalizer, writer, NullLogger<EpubController>.Instance);

        var result = await controller.Parse(
            new ParseEpubRequest("fixture.epub", Tts: true, TtsLanguage: language),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, normalizer.CallCount);
        Assert.Equal(language, normalizer.Options?.Language);
        Assert.Same(parsed, normalizer.Book);
        Assert.Same(normalized, writer.Book);
    }

    [Fact]
    public async Task Parse_DoesNotPublishWhenTtsNormalizationFails()
    {
        var writer = new StubWriter(null!);
        var controller = new EpubController(
            new StubParser(Book()),
            new StubNormalizer(exception: new EpubParseException(
                EpubParseErrorKind.InvalidRequest,
                "Safe normalization detail.")),
            writer,
            NullLogger<EpubController>.Instance);

        var result = await controller.Parse(
            new ParseEpubRequest("fixture.epub", Tts: true, TtsLanguage: "pt"),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(400, objectResult.StatusCode);
        Assert.Equal("Safe normalization detail.", problem.Detail);
        Assert.Equal(0, writer.CallCount);
    }

    [Theory]
    [InlineData(EpubParseErrorKind.InvalidRequest, 400)]
    [InlineData(EpubParseErrorKind.OutputConflict, 409)]
    [InlineData(EpubParseErrorKind.UnsupportedContent, 415)]
    [InlineData(EpubParseErrorKind.InvalidEpub, 422)]
    [InlineData(EpubParseErrorKind.UnsupportedStructure, 422)]
    public async Task Parse_MapsExpectedErrorsToSanitizedProblemDetails(EpubParseErrorKind kind, int status)
    {
        var controller = new EpubController(
            new ThrowingParser(new EpubParseException(kind, "Safe detail.")),
            new StubNormalizer(),
            new StubWriter(null!),
            NullLogger<EpubController>.Instance);

        var result = await controller.Parse(new ParseEpubRequest("fixture.epub"), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(status, objectResult.StatusCode);
        Assert.Equal("Safe detail.", problem.Detail);
    }

    [Fact]
    public void Health_DoesNotInvokeParser()
    {
        var controller = new EpubController(
            new ThrowingParser(new InvalidOperationException()),
            new StubNormalizer(),
            new StubWriter(null!),
            NullLogger<EpubController>.Instance);

        Assert.IsType<OkObjectResult>(controller.Health());
    }

    private static ParsedEpubBook Book(string language = "en") =>
        new("fixture.epub", "Fixture", language, [new ParsedChapter(1, ["Private prose."])]);

    private static ParseEpubResponse Response() =>
        new(
            "fixture.epub", "Fixture", "en", "/data/output/fixture", 1,
            [new ParsedChapterSummary(1, "chapter-001.txt", 29)]);

    private sealed class StubParser(ParsedEpubBook result) : IEpubChapterParser
    {
        public int CallCount { get; private set; }

        public Task<ParsedEpubBook> ParseAsync(string fileName, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingParser(Exception exception) : IEpubChapterParser
    {
        public Task<ParsedEpubBook> ParseAsync(string fileName, CancellationToken cancellationToken = default) =>
            Task.FromException<ParsedEpubBook>(exception);
    }

    private sealed class StubNormalizer(
        ParsedEpubBook? result = null,
        Exception? exception = null) : ITtsTextNormalizer
    {
        public int CallCount { get; private set; }
        public ParsedEpubBook? Book { get; private set; }
        public TtsTextOptions? Options { get; private set; }

        public ParsedEpubBook Normalize(ParsedEpubBook book, TtsTextOptions options)
        {
            CallCount++;
            Book = book;
            Options = options;
            if (exception is not null)
            {
                throw exception;
            }

            return result ?? book;
        }
    }

    private sealed class StubWriter(ParseEpubResponse result) : IChapterOutputWriter
    {
        public int CallCount { get; private set; }
        public ParsedEpubBook? Book { get; private set; }

        public Task<ParseEpubResponse> PublishAsync(ParsedEpubBook book, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Book = book;
            return Task.FromResult(result);
        }
    }
}
