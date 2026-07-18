using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Controllers;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Controllers;

public class ChatControllerTests
{
    [Fact]
    public async Task Send_WhenUserHasBooks_PassesBookToolsToAgent()
    {
        var userId = "user-1";
        var agent = new FakeChatOrchestratorAgent();
        var db = CreateDbContext();
        db.Books.Add(new Book
        {
            UserId = userId,
            Title = "Dune",
            SourceBookTitle = "Dune",
            Author = "Frank Herbert",
            NormalizedTitle = "dune",
            NormalizedAuthor = "frankherbert"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(agent, new FakeCacheHandler(), new FakeBookContextAgentTool(), userId, db);

        await controller.Send(new ChatSendRequest { Message = "Tell me about Dune" }, CancellationToken.None);

        Assert.NotNull(agent.LastTools);
        Assert.Equal(3, agent.LastTools.Count);
        Assert.Equal("GenerateBookContext", agent.LastTools[0].Name);
        Assert.Equal("GetBookNotesWithAnalysis", agent.LastTools[1].Name);
        Assert.Equal("GetRelevantBookNotes", agent.LastTools[2].Name);
    }

    [Fact]
    public async Task SetAgent_WithPremium_PersistsActiveAgentAndReturnsIndicator()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        var sessionId = Guid.NewGuid();
        await cache.SetAsync($"activesessionid:{userId}", sessionId.ToString("D"), TimeSpan.FromMinutes(5));
        await cache.SetAsync($"agentsession:{userId}:{sessionId:D}", """{"session":"existing"}""", TimeSpan.FromMinutes(5));

        var controller = CreateController(new FakeChatOrchestratorAgent(), cache, new FakeBookContextAgentTool(), userId);

        var result = await controller.SetAgent("premium", CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_AgentIndicator", partial.ViewName);
        Assert.Equal("premium", partial.Model);
        Assert.Equal("premium", await cache.GetAsync($"activeagent:{userId}"));
        Assert.Equal(sessionId.ToString("D"), await cache.GetAsync($"activesessionid:{userId}"));
        Assert.Equal("""{"session":"existing"}""", await cache.GetAsync($"agentsession:{userId}:{sessionId:D}"));
    }

    [Fact]
    public async Task Send_WhenUserHasNoBooks_PassesNullToolsAndSavesSession()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        var agent = new FakeChatOrchestratorAgent();
        var controller = CreateController(agent, cache, new FakeBookContextAgentTool(), userId);

        var result = await controller.Send(new ChatSendRequest { Message = "Hello" }, CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_BotMessage", partial.ViewName);
        Assert.Equal("Hello", agent.LastMessage);
        Assert.NotNull(agent.LastTools);
        Assert.Equal(3, agent.LastTools.Count);
        Assert.Same(FakeChatAgentProvider.FreeAgent, agent.LastAgent);
        Assert.True(Guid.TryParse(await cache.GetAsync($"activesessionid:{userId}"), out var sessionId));
        Assert.Equal("""{"session":"updated"}""", await cache.GetAsync($"agentsession:{userId}:{sessionId:D}"));
        Assert.Equal(2, await controllerDbCountAsync(controller, userId));
    }

    [Fact]
    public async Task Send_WhenActiveAgentIsPremium_UsesPremiumAgentAndPersistsAgentType()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        await cache.SetAsync($"activeagent:{userId}", "premium", TimeSpan.FromMinutes(5));
        var agent = new FakeChatOrchestratorAgent { ResponseText = "Premium answer" };
        var bookContextTool = new FakeBookContextAgentTool();
        var db = CreateDbContext();
        var controller = CreateController(agent, cache, bookContextTool, userId, db);

        var result = await controller.Send(new ChatSendRequest { Message = "Hello" }, CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsType<BotMessageViewModel>(partial.Model);
        Assert.Equal("premium", model.AgentType);
        Assert.Equal("Premium", model.AgentLabel);
        Assert.Same(FakeChatAgentProvider.PremiumAgent, agent.LastAgent);
        Assert.Equal("premium", bookContextTool.LastAgentKey);

        var assistant = await db.ChatMessages.SingleAsync(x => x.Role == "assistant");
        Assert.Equal("premium", assistant.AgentType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bad")]
    public async Task Send_WhenActiveAgentMissingOrInvalid_DefaultsToFree(string? cachedAgent)
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        if (cachedAgent is not null)
            await cache.SetAsync($"activeagent:{userId}", cachedAgent, TimeSpan.FromMinutes(5));

        var agent = new FakeChatOrchestratorAgent();
        var controller = CreateController(agent, cache, new FakeBookContextAgentTool(), userId);

        await controller.Send(new ChatSendRequest { Message = "Hello" }, CancellationToken.None);

        Assert.Same(FakeChatAgentProvider.FreeAgent, agent.LastAgent);
    }

    [Fact]
    public async Task Send_WhenFormAgentDiffersFromCache_UsesFormAgentAndUpdatesCache()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        await cache.SetAsync($"activeagent:{userId}", "premium", TimeSpan.FromMinutes(5));
        var agent = new FakeChatOrchestratorAgent();
        var controller = CreateController(agent, cache, new FakeBookContextAgentTool(), userId);

        await controller.Send(new ChatSendRequest { Message = "Hello", AgentKey = "free" }, CancellationToken.None);

        Assert.Same(FakeChatAgentProvider.FreeAgent, agent.LastAgent);
        Assert.Equal("free", await cache.GetAsync($"activeagent:{userId}"));
    }

    [Fact]
    public async Task Send_WhenUserHasBooks_DoesNotIncludeBookListInInstructions()
    {
        var userId = "user-1";
        var agent = new FakeChatOrchestratorAgent();
        var db = CreateDbContext();
        db.Books.Add(new Book
        {
            UserId = userId,
            Title = "Foundation",
            SourceBookTitle = "Foundation",
            Author = "Isaac Asimov",
            NormalizedTitle = "foundation",
            NormalizedAuthor = "isaacasimov"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(agent, new FakeCacheHandler(), new FakeBookContextAgentTool(), userId, db);

        await controller.Send(new ChatSendRequest { Message = "Hello" }, CancellationToken.None);

        Assert.DoesNotContain("Title: \"Foundation\" | Author: \"Isaac Asimov\"", agent.LastInstructions);
        Assert.DoesNotContain("User's book library", agent.LastInstructions);
        Assert.Contains("call the GenerateBookContext tool before answering", agent.LastInstructions);
        Assert.Contains("GetBookNotesWithAnalysis", agent.LastInstructions);
        Assert.Contains("personal notes, highlights, or annotations", agent.LastInstructions);
        Assert.Contains("Do not say a book is missing from the library unless GenerateBookContext returns a not found result", agent.LastInstructions);
    }

    [Fact]
    public async Task Send_WhenUserHasMoreThanTwentyFiveBooks_DoesNotEnumerateOlderBooksInInstructions()
    {
        var userId = "user-1";
        var agent = new FakeChatOrchestratorAgent();
        var db = CreateDbContext();

        db.Books.Add(new Book
        {
            UserId = userId,
            Title = "Dick, Philip K - Gather Yourselves Together",
            SourceBookTitle = "Dick, Philip K - Gather Yourselves Together",
            Author = "Philip K Dick",
            NormalizedTitle = "dickphilipkgatheryourselvestogether",
            NormalizedAuthor = "philipkdick",
            UpdatedAt = DateTime.UtcNow.AddDays(-40)
        });

        for (var i = 0; i < 30; i++)
        {
            db.Books.Add(new Book
            {
                UserId = userId,
                Title = $"Recent Book {i}",
                SourceBookTitle = $"Recent Book {i}",
                Author = "Test Author",
                NormalizedTitle = $"recentbook{i}",
                NormalizedAuthor = "testauthor",
                UpdatedAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        await db.SaveChangesAsync();

        var controller = CreateController(agent, new FakeCacheHandler(), new FakeBookContextAgentTool(), userId, db);

        await controller.Send(new ChatSendRequest { Message = "Tell me about Gather Yourselves Together" }, CancellationToken.None);

        Assert.DoesNotContain("Dick, Philip K - Gather Yourselves Together", agent.LastInstructions);
    }

    [Fact]
    public async Task Chat_WhenSessionIdExists_ReadsMessagesFromDb()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        var sessionId = Guid.NewGuid();
        await cache.SetAsync($"activesessionid:{userId}", sessionId.ToString("D"), TimeSpan.FromMinutes(5));
        var db = CreateDbContext();
        db.ChatMessages.AddRange(
            new WebApp.Models.ChatMessage
            {
                UserId = userId,
                SessionId = sessionId,
                Role = "user",
                Content = "Tell me about Leviathan Wakes",
                DisplayOrder = 1
            },
            new WebApp.Models.ChatMessage
            {
                UserId = userId,
                SessionId = sessionId,
                Role = "assistant",
                Content = "Leviathan Wakes context.",
                AgentType = "free",
                DisplayOrder = 2,
                ResponseTimeMs = 24000
            });
        await db.SaveChangesAsync();
        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            cache,
            new FakeBookContextAgentTool(),
            userId,
            db);

        var result = await controller.Chat(CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        var entries = Assert.IsAssignableFrom<List<ChatEntry>>(partial.Model);
        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal("user", entry.Role);
                Assert.Equal("Tell me about Leviathan Wakes", entry.Content);
            },
            entry =>
            {
                Assert.Equal("assistant", entry.Role);
                Assert.Contains("Leviathan Wakes context.", entry.Content);
                Assert.Equal(24000, entry.ResponseTimeMs);
                Assert.Equal("free", entry.AgentType);
                Assert.Equal("Free", entry.AgentLabel);
            });
    }

    [Fact]
    public async Task Send_WhenPremiumAgentThrows_ReturnsErrorWithoutUsingFreeAgent()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        await cache.SetAsync($"activeagent:{userId}", "premium", TimeSpan.FromMinutes(5));
        var agent = new ThrowingChatOrchestratorAgent();
        var controller = CreateController(agent, cache, new FakeBookContextAgentTool(), userId);

        var result = await controller.Send(new ChatSendRequest { Message = "Hello" }, CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_BotMessage", partial.ViewName);
        Assert.Contains("Error", Assert.IsType<BotMessageViewModel>(partial.Model).HtmlContent);
        Assert.Same(FakeChatAgentProvider.PremiumAgent, agent.LastAgent);
    }

    [Fact]
    public async Task Reset_RemovesSession()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        var sessionId = Guid.NewGuid();
        await cache.SetAsync($"activesessionid:{userId}", sessionId.ToString("D"), TimeSpan.FromMinutes(5));
        await cache.SetAsync($"agentsession:{userId}:{sessionId:D}", """{"session":"updated"}""", TimeSpan.FromMinutes(5));
        await cache.SetAsync($"agentcontext:{userId}:{sessionId:D}", """{"usagePct":50}""", TimeSpan.FromMinutes(5));
        var db = CreateDbContext();
        db.ChatMessages.Add(new WebApp.Models.ChatMessage
        {
            UserId = userId,
            SessionId = sessionId,
            Role = "assistant",
            Content = "Cached answer",
            DisplayOrder = 1,
            ResponseTimeMs = 1000
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            cache,
            new FakeBookContextAgentTool(),
            userId,
            db);

        var result = await controller.Reset(CancellationToken.None);

        Assert.IsType<PartialViewResult>(result);
        Assert.Null(await cache.GetAsync($"activesessionid:{userId}"));
        Assert.Null(await cache.GetAsync($"agentsession:{userId}:{sessionId:D}"));
        Assert.Null(await cache.GetAsync($"agentcontext:{userId}:{sessionId:D}"));
        Assert.Empty(await db.ChatMessages.Where(x => x.UserId == userId && x.SessionId == sessionId).ToListAsync());
    }

    [Fact]
    public async Task Send_WhenMessageIsEmpty_ReturnsEmptyContent()
    {
        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            new FakeCacheHandler(),
            new FakeBookContextAgentTool(),
            "user-1");

        var result = await controller.Send(new ChatSendRequest { Message = "" }, CancellationToken.None);

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

        var result = await controller.Send(new ChatSendRequest { Message = "Hello" }, CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_BotMessage", partial.ViewName);
        Assert.Contains("Error", Assert.IsType<BotMessageViewModel>(partial.Model).HtmlContent);
    }

    [Fact]
    public async Task GetMessageAudio_WhenAudioExists_ReturnsFileResult()
    {
        var messageId = Guid.NewGuid();
        var audioService = new FakeChatMessageAudioService { ReturnValue = (new byte[] { 0x52, 0x49 }, "audio/wav") };
        var controller = CreateController(new FakeChatOrchestratorAgent(), new FakeCacheHandler(), new FakeBookContextAgentTool(), "user-1", audioService: audioService);

        var result = await controller.GetMessageAudio(messageId, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("audio/wav", fileResult.ContentType);
        Assert.Equal(new byte[] { 0x52, 0x49 }, fileResult.FileContents);
    }

    [Fact]
    public async Task GetMessageAudio_WhenAudioNotFound_ReturnsNotFound()
    {
        var audioService = new FakeChatMessageAudioService { ReturnValue = null };
        var controller = CreateController(new FakeChatOrchestratorAgent(), new FakeCacheHandler(), new FakeBookContextAgentTool(), "user-1", audioService: audioService);

        var result = await controller.GetMessageAudio(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetMessageAudio_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var controller = new ChatController(
            new FakeChatOrchestratorAgent(),
            new FakeChatAgentProvider(),
            new FakeCacheHandler(),
            new FakeBookContextAgentTool(),
            new FakeBookNotesAgentTool(),
            new FakeBookNoteSearchAgentTool(),
            CreateDbContext(),
            new FakeChatMessageAudioService(),
            NullLogger<ChatController>.Instance,
            new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal() }
        };

        var result = await controller.GetMessageAudio(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    private static async Task<int> controllerDbCountAsync(ChatController controller, string userId)
    {
        var dbField = typeof(ChatController).GetField("_db", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var db = Assert.IsAssignableFrom<AppDbContext>(dbField?.GetValue(controller));
        return await db.ChatMessages.CountAsync(x => x.UserId == userId);
    }

    private static ChatController CreateController(
        IChatOrchestratorAgent agent,
        ICacheHandler cache,
        IBookContextAgentTool bookContextTool,
        string userId,
        AppDbContext? db = null,
        IChatMessageAudioService? audioService = null)
    {
        db ??= CreateDbContext();
        var controller = new ChatController(
            agent,
            new FakeChatAgentProvider(),
            cache,
            bookContextTool,
            new FakeBookNotesAgentTool(),
            new FakeBookNoteSearchAgentTool(),
            db,
            audioService ?? new FakeChatMessageAudioService(),
            NullLogger<ChatController>.Instance,
            new ConfigurationBuilder().Build());
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
        public string ResponseText { get; init; } = "Grounded answer";
        public AIAgent? LastAgent { get; private set; }
        public string? LastMessage { get; private set; }
        public string? LastInstructions { get; private set; }
        public IReadOnlyList<AITool>? LastTools { get; private set; }

        public Task<ChatAgentRunResult> RunAsync(AIAgent agent, string message, string? sessionJson, string? instructions, IReadOnlyList<AITool>? tools = null, CancellationToken ct = default)
        {
            LastAgent = agent;
            LastMessage = message;
            LastInstructions = instructions;
            LastTools = tools;
            return Task.FromResult(new ChatAgentRunResult(ResponseText, """{"session":"updated"}""", 100, 50, 100, 50, 100, 50, 1, 1200));
        }
    }

    private sealed class FakeChatAgentProvider : IChatAgentProvider
    {
        public static readonly AIAgent FreeAgent = new ChatClientAgent(new FakeChatClient(), name: "FreeAgent");
        public static readonly AIAgent PremiumAgent = new ChatClientAgent(new FakeChatClient(), name: "PremiumAgent");

        public AIAgent GetAgent(string agentKey) => agentKey switch
        {
            "premium" => PremiumAgent,
            "free" => FreeAgent,
            _ => throw new InvalidOperationException($"Unknown chat agent key '{agentKey}'.")
        };
    }

    private sealed class FakeBookContextAgentTool : IBookContextAgentTool
    {
        public string? LastAgentKey { get; private set; }

        public AIFunction Create(string userId, string agentKey)
        {
            LastAgentKey = agentKey;
            return
            AIFunctionFactory.Create(
                (string bookTitle) => Task.FromResult<string>("context"),
                name: "GenerateBookContext");
        }
    }

    private sealed class FakeBookNotesAgentTool : IBookNotesAgentTool
    {
        public AIFunction Create(string userId) =>
            AIFunctionFactory.Create(
                (string bookTitle) => Task.FromResult<string>("notes"),
                name: "GetBookNotesWithAnalysis");
    }

    private sealed class FakeBookNoteSearchAgentTool : IBookNoteSearchAgentTool
    {
        public AIFunction Create(string userId) =>
            AIFunctionFactory.Create(
                (string bookTitle, string searchQuery) => Task.FromResult<string>("relevant notes"),
                name: "GetRelevantBookNotes");
    }

    private sealed class ThrowingChatOrchestratorAgent : IChatOrchestratorAgent
    {
        public AIAgent? LastAgent { get; private set; }

        public Task<ChatAgentRunResult> RunAsync(AIAgent agent, string message, string? sessionJson, string? instructions, IReadOnlyList<AITool>? tools = null, CancellationToken ct = default)
        {
            LastAgent = agent;
            throw new InvalidOperationException("agent failure");
        }
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "ok")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class FakeChatMessageAudioService : WebApp.Services.IChatMessageAudioService
    {
        public (byte[] WavBytes, string ContentType)? ReturnValue { get; set; } = (new byte[] { 1, 2, 3 }, "audio/wav");

        public Task<(byte[] WavBytes, string ContentType)?> GetOrCreateAudioAsync(
            string userId, Guid messageId, CancellationToken ct = default)
            => Task.FromResult(ReturnValue);
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
            builder.Ignore<BookEmbedding>();
            builder.Ignore<BookNoteEmbedding>();
        }
    }
}
