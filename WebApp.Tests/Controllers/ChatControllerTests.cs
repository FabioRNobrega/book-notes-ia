using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Controllers;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Controllers;

public class ChatControllerTests
{
    [Fact]
    public async Task Send_WhenUserHasBooks_PassesGenerateBookContextToolToAgent()
    {
        var userId = "user-1";
        var agent = new FakeChatOrchestratorAgent();
        var db = CreateDbContext();
        db.Books.Add(new Book
        {
            UserId = userId,
            Title = "Dune",
            Author = "Frank Herbert",
            NormalizedTitle = "dune",
            NormalizedAuthor = "frankherbert"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(agent, new FakeCacheHandler(), new FakeBookContextAgentTool(), userId, db);

        await controller.Send("Tell me about Dune", CancellationToken.None);

        Assert.NotNull(agent.LastTools);
        Assert.Single(agent.LastTools);
        Assert.Equal("GenerateBookContext", agent.LastTools[0].Name);
    }

    [Fact]
    public async Task Send_WhenUserHasNoBooks_PassesNullToolsAndSavesSession()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        var agent = new FakeChatOrchestratorAgent();
        var controller = CreateController(agent, cache, new FakeBookContextAgentTool(), userId);

        var result = await controller.Send("Hello", CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_BotMessage", partial.ViewName);
        Assert.Equal("Hello", agent.LastMessage);
        Assert.Null(agent.LastTools);
        Assert.Equal("""{"session":"updated"}""", await cache.GetAsync($"agentsession:{userId}"));
    }

    [Fact]
    public async Task Send_WhenUserHasBooks_IncludesBookListInInstructions()
    {
        var userId = "user-1";
        var agent = new FakeChatOrchestratorAgent();
        var db = CreateDbContext();
        db.Books.Add(new Book
        {
            UserId = userId,
            Title = "Foundation",
            Author = "Isaac Asimov",
            NormalizedTitle = "foundation",
            NormalizedAuthor = "isaacasimov"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(agent, new FakeCacheHandler(), new FakeBookContextAgentTool(), userId, db);

        await controller.Send("Hello", CancellationToken.None);

        Assert.Contains("Foundation by Isaac Asimov", agent.LastInstructions);
    }

    [Fact]
    public async Task Reset_RemovesSession()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        await cache.SetAsync($"agentsession:{userId}", """{"session":"updated"}""", TimeSpan.FromMinutes(5));

        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            cache,
            new FakeBookContextAgentTool(),
            userId);

        var result = await controller.Reset(CancellationToken.None);

        Assert.IsType<PartialViewResult>(result);
        Assert.Null(await cache.GetAsync($"agentsession:{userId}"));
    }

    [Fact]
    public async Task Send_WhenMessageIsEmpty_ReturnsEmptyContent()
    {
        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            new FakeCacheHandler(),
            new FakeBookContextAgentTool(),
            "user-1");

        var result = await controller.Send("", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("", content.Content);
    }

    [Fact]
    public async Task Send_WhenAgentThrows_ReturnsErrorBotMessage()
    {
        var controller = CreateController(
            new ThrowingChatOrchestratorAgent(),
            new FakeCacheHandler(),
            new FakeBookContextAgentTool(),
            "user-1");

        var result = await controller.Send("Hello", CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_BotMessage", partial.ViewName);
        Assert.Contains("Error", Assert.IsType<string>(partial.Model));
    }

    private static ChatController CreateController(
        IChatOrchestratorAgent agent,
        ICacheHandler cache,
        IBookContextAgentTool bookContextTool,
        string userId,
        AppDbContext? db = null)
    {
        db ??= CreateDbContext();
        var controller = new ChatController(agent, cache, bookContextTool, db, NullLogger<ChatController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId)],
                    "TestAuth"))
            }
        };
        return controller;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }

    private sealed class FakeChatOrchestratorAgent : IChatOrchestratorAgent
    {
        public string? LastMessage { get; private set; }
        public string? LastInstructions { get; private set; }
        public IReadOnlyList<AITool>? LastTools { get; private set; }

        public Task<ChatAgentRunResult> RunAsync(string message, string? sessionJson, string? instructions, IReadOnlyList<AITool>? tools = null, CancellationToken ct = default)
        {
            LastMessage = message;
            LastInstructions = instructions;
            LastTools = tools;
            return Task.FromResult(new ChatAgentRunResult("Grounded answer", """{"session":"updated"}"""));
        }
    }

    private sealed class FakeBookContextAgentTool : IBookContextAgentTool
    {
        public AIFunction Create(string userId) =>
            AIFunctionFactory.Create(
                (string bookTitle) => Task.FromResult<string>("context"),
                name: "GenerateBookContext");
    }

    private sealed class ThrowingChatOrchestratorAgent : IChatOrchestratorAgent
    {
        public Task<ChatAgentRunResult> RunAsync(string message, string? sessionJson, string? instructions, IReadOnlyList<AITool>? tools = null, CancellationToken ct = default)
            => throw new InvalidOperationException("agent failure");
    }

    private sealed class FakeCacheHandler : ICacheHandler
    {
        private readonly Dictionary<string, string> _store = [];

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task SetObjectAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        {
            _store[key] = System.Text.Json.JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }
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
