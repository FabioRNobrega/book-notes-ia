using System.Net;
using Microsoft.Extensions.Logging;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class OpenLibraryServiceTests
{
    [Fact]
    public async Task GetSynopsisAsync_ReturnsPlainStringDescription()
    {
        var service = CreateService(
            JsonResponse("""{ "docs": [{ "key": "/works/OL893415W" }] }"""),
            JsonResponse("""{ "description": "plain string" }"""));

        var result = await service.GetSynopsisAsync("Dune", "Frank Herbert");

        Assert.Equal("plain string", result);
    }

    [Fact]
    public async Task GetSynopsisAsync_ReturnsObjectDescription()
    {
        var service = CreateService(
            JsonResponse("""{ "docs": [{ "key": "/works/OL893415W" }] }"""),
            JsonResponse("""{ "description": { "type": "/type/text", "value": "object value" } }"""));

        var result = await service.GetSynopsisAsync("Dune", "Frank Herbert");

        Assert.Equal("object value", result);
    }

    [Fact]
    public async Task GetSynopsisAsync_ReturnsNullWhenDocsEmpty()
    {
        var service = CreateService(JsonResponse("""{ "docs": [] }"""));

        var result = await service.GetSynopsisAsync("Missing", "Nobody");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSynopsisAsync_ReturnsNullOnHttpError()
    {
        var logger = new TestLogger<OpenLibraryService>();
        var service = CreateService(logger, _ => throw new HttpRequestException("Open Library unavailable."));

        var result = await service.GetSynopsisAsync("Dune", "Frank Herbert");

        Assert.Null(result);
        Assert.Contains(LogLevel.Warning, logger.LogLevels);
    }

    [Fact]
    public async Task GetSynopsisAsync_ReturnsNullWhenDescriptionAbsent()
    {
        var service = CreateService(
            JsonResponse("""{ "docs": [{ "key": "/works/OL893415W" }] }"""),
            JsonResponse("""{ "title": "Dune" }"""));

        var result = await service.GetSynopsisAsync("Dune", "Frank Herbert");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSynopsisAsync_ReturnsNullOnInvalidJson()
    {
        var logger = new TestLogger<OpenLibraryService>();
        var service = CreateService(
            logger,
            JsonResponse("""{ "docs": [{ "key": "/works/OL893415W" }] }"""),
            JsonResponse("""{ """));

        var result = await service.GetSynopsisAsync("Dune", "Frank Herbert");

        Assert.Null(result);
        Assert.Contains(LogLevel.Warning, logger.LogLevels);
    }

    private static OpenLibraryService CreateService(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) =>
        CreateService(new TestLogger<OpenLibraryService>(), responses);

    private static OpenLibraryService CreateService(
        TestLogger<OpenLibraryService> logger,
        params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    {
        var handler = new StubHttpMessageHandler(responses);
        return new OpenLibraryService(new HttpClient(handler), logger);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> JsonResponse(string json) =>
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };

    private sealed class StubHttpMessageHandler(IEnumerable<Func<HttpRequestMessage, HttpResponseMessage>> responses)
        : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException("No HTTP response configured.");

            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogLevel> LogLevels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogLevels.Add(logLevel);
        }
    }
}
