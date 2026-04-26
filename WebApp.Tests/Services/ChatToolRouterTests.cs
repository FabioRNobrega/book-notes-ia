using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class ChatToolRouterTests
{
    [Fact]
    public async Task RouteAsync_WhenNoBooksExist_ReturnsNoneWithoutCallingLlm()
    {
        await using var db = CreateDbContext();
        var client = new FakeChatClient("""{"tool":"GenerateBookContext","bookId":"00000000-0000-0000-0000-000000000001"}""");
        var router = new ChatToolRouter(db, client, NullLogger<ChatToolRouter>.Instance);

        var result = await router.RouteAsync("user-1", "generate context for Dune", CancellationToken.None);

        Assert.Equal("none", result.Tool);
        Assert.Null(result.BookId);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task RouteAsync_WhenMessageContainsContextKeywordAndMatchingTitle_ReturnsGenerateBookContextWithoutLlm()
    {
        await using var db = CreateDbContext();
        var book = new Book
        {
            UserId = "user-1",
            Title = "Dune",
            Author = "Frank Herbert",
            NormalizedTitle = "dune",
            NormalizedAuthor = "frankherbert"
        };
        db.Books.Add(book);
        await db.SaveChangesAsync();

        var client = new FakeChatClient("{}");
        var router = new ChatToolRouter(db, client, NullLogger<ChatToolRouter>.Instance);

        var result = await router.RouteAsync("user-1", "generate context for Dune", CancellationToken.None);

        Assert.Equal("GenerateBookContext", result.Tool);
        Assert.Equal(book.Id, result.BookId);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task RouteAsync_WhenMessageContainsBackgroundKeywordAndMatchingAuthor_ReturnsGenerateBookContextWithoutLlm()
    {
        await using var db = CreateDbContext();
        var book = new Book
        {
            UserId = "user-1",
            Title = "Foundation",
            Author = "Isaac Asimov",
            NormalizedTitle = "foundation",
            NormalizedAuthor = "isaacasimov"
        };
        db.Books.Add(book);
        await db.SaveChangesAsync();

        var client = new FakeChatClient("{}");
        var router = new ChatToolRouter(db, client, NullLogger<ChatToolRouter>.Instance);

        var result = await router.RouteAsync("user-1", "give me background about Isaac Asimov", CancellationToken.None);

        Assert.Equal("GenerateBookContext", result.Tool);
        Assert.Equal(book.Id, result.BookId);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task RouteAsync_WhenMessageHasNoContextKeyword_FallsThroughToLlmAndReturnsToolDecision()
    {
        await using var db = CreateDbContext();
        var bookId = Guid.NewGuid();
        db.Books.Add(new Book
        {
            Id = bookId,
            UserId = "user-1",
            Title = "Dune",
            Author = "Frank Herbert",
            NormalizedTitle = "dune",
            NormalizedAuthor = "frankherbert"
        });
        await db.SaveChangesAsync();

        var llmResponse = $$"""{ "tool": "GenerateBookContext", "bookId": "{{bookId}}" }""";
        var client = new FakeChatClient(llmResponse);
        var router = new ChatToolRouter(db, client, NullLogger<ChatToolRouter>.Instance);

        var result = await router.RouteAsync("user-1", "tell me about Dune", CancellationToken.None);

        Assert.Equal("GenerateBookContext", result.Tool);
        Assert.Equal(bookId, result.BookId);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task RouteAsync_WhenLlmReturnsNoneDecision_ReturnsNone()
    {
        await using var db = CreateDbContext();
        db.Books.Add(new Book
        {
            UserId = "user-1",
            Title = "Dune",
            Author = "Frank Herbert",
            NormalizedTitle = "dune",
            NormalizedAuthor = "frankherbert"
        });
        await db.SaveChangesAsync();

        var client = new FakeChatClient("""{"tool":"none","bookId":null}""");
        var router = new ChatToolRouter(db, client, NullLogger<ChatToolRouter>.Instance);

        var result = await router.RouteAsync("user-1", "what is your favorite food?", CancellationToken.None);

        Assert.Equal("none", result.Tool);
        Assert.Null(result.BookId);
    }

    [Fact]
    public async Task RouteAsync_WhenLlmThrows_ReturnsNoneGracefully()
    {
        await using var db = CreateDbContext();
        db.Books.Add(new Book
        {
            UserId = "user-1",
            Title = "Dune",
            Author = "Frank Herbert",
            NormalizedTitle = "dune",
            NormalizedAuthor = "frankherbert"
        });
        await db.SaveChangesAsync();

        var router = new ChatToolRouter(db, new ThrowingChatClient(), NullLogger<ChatToolRouter>.Instance);

        var result = await router.RouteAsync("user-1", "what is your favorite food?", CancellationToken.None);

        Assert.Equal("none", result.Tool);
        Assert.Null(result.BookId);
    }

    [Fact]
    public async Task RouteAsync_WhenLlmReturnsMalformedJson_ReturnsNone()
    {
        await using var db = CreateDbContext();
        db.Books.Add(new Book
        {
            UserId = "user-1",
            Title = "Dune",
            Author = "Frank Herbert",
            NormalizedTitle = "dune",
            NormalizedAuthor = "frankherbert"
        });
        await db.SaveChangesAsync();

        var client = new FakeChatClient("I cannot decide which tool to use here!!!");
        var router = new ChatToolRouter(db, client, NullLogger<ChatToolRouter>.Instance);

        var result = await router.RouteAsync("user-1", "what is your favorite food?", CancellationToken.None);

        Assert.Equal("none", result.Tool);
        Assert.Null(result.BookId);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private sealed class FakeChatClient(string responseText) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ChatClientMetadata Metadata => new(null, null, null);

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class ThrowingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("LLM unreachable");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ChatClientMetadata Metadata => new(null, null, null);

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class TestAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<UserProfile>().Ignore(x => x.ReadingLanguages);
            builder.Entity<UserProfile>().Ignore(x => x.LearningStyle);
            builder.Entity<UserProfile>().Ignore(x => x.LovedGenres);
            builder.Entity<UserProfile>().Ignore(x => x.DislikedGenres);
        }
    }
}
