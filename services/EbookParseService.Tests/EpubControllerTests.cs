using EbookParseService.Api.Controllers;
using EbookParseService.Api.Models;
using EbookParseService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace EbookParseService.Tests;

public sealed class EpubControllerTests
{
    [Fact]
    public async Task Parse_ReturnsSafeSuccessSummary()
    {
        var book = new ParsedEpubBook("fixture.epub", "Fixture", "en", [new ParsedChapter(1, ["Private prose."])]);
        var response = new ParseEpubResponse(
            "fixture.epub", "Fixture", "en", "/data/output/fixture", 1,
            [new ParsedChapterSummary(1, "chapter-001.txt", 29)]);
        var controller = new EpubController(
            new StubParser(book), new StubWriter(response), NullLogger<EpubController>.Instance);

        var result = await controller.Parse(new ParseEpubRequest("fixture.epub"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<ParseEpubResponse>(ok.Value);
        Assert.Equal(response, model);
        Assert.DoesNotContain("Private prose", System.Text.Json.JsonSerializer.Serialize(model), StringComparison.Ordinal);
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
            new StubWriter(null!),
            NullLogger<EpubController>.Instance);

        Assert.IsType<OkObjectResult>(controller.Health());
    }

    private sealed class StubParser(ParsedEpubBook result) : IEpubChapterParser
    {
        public Task<ParsedEpubBook> ParseAsync(string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingParser(Exception exception) : IEpubChapterParser
    {
        public Task<ParsedEpubBook> ParseAsync(string fileName, CancellationToken cancellationToken = default) =>
            Task.FromException<ParsedEpubBook>(exception);
    }

    private sealed class StubWriter(ParseEpubResponse result) : IChapterOutputWriter
    {
        public Task<ParseEpubResponse> PublishAsync(ParsedEpubBook book, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
