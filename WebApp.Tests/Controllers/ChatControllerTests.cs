using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Controllers;
using WebApp.Services;

namespace WebApp.Tests.Controllers;

public class ChatControllerTests
{
    [Fact]
    public async Task Send_WhenToolIsSelected_AppendsWorkingContextAndPersistsSession()
    {
        var userId = "user-1";
        var bookId = Guid.NewGuid();
        var cache = new FakeCacheHandler();
        await cache.SetAsync($"agentprofile:{userId}", """{"nickname":"Ana","preferred_language":"pt-BR"}""", TimeSpan.FromMinutes(5));

        var agent = new FakeChatOrchestratorAgent();
        var toolService = new FakeBookContextService
        {
            GenerateResult = new GenerateBookContextToolResult(
                bookId,
                "Dune",
                "Frank Herbert",
                "Arrakis background.",
                "[GenerateBookContext]\nBook: Dune\nSummary: Arrakis background.")
        };
        var router = new FakeChatToolRouter(new ChatToolRouteDecision("GenerateBookContext", bookId));
        var controller = CreateController(agent, cache, toolService, router, userId);

        var result = await controller.Send("Generate context for Dune", CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_BotMessage", partial.ViewName);
        Assert.Contains("<p>Grounded answer</p>", Assert.IsType<string>(partial.Model));

        Assert.Equal("Generate context for Dune", agent.LastMessage);
        Assert.Contains("Working context gathered from tools:", agent.LastInstructions);
        Assert.Contains("Arrakis background.", agent.LastInstructions);
        Assert.Equal("[GenerateBookContext]\nBook: Dune\nSummary: Arrakis background.", await cache.GetAsync($"agentcontext:{userId}"));
        Assert.Equal("""{"session":"updated"}""", await cache.GetAsync($"agentsession:{userId}"));
        Assert.Equal(bookId, toolService.LastGenerateBookId);
    }

    [Fact]
    public async Task Reset_RemovesSessionAndWorkingContext()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        await cache.SetAsync($"agentsession:{userId}", """{"session":"updated"}""", TimeSpan.FromMinutes(5));
        await cache.SetAsync($"agentcontext:{userId}", "tool context", TimeSpan.FromMinutes(5));

        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            cache,
            new FakeBookContextService(),
            new FakeChatToolRouter(new ChatToolRouteDecision("none", null)),
            userId);

        var result = await controller.Reset(CancellationToken.None);

        Assert.IsType<PartialViewResult>(result);
        Assert.Null(await cache.GetAsync($"agentsession:{userId}"));
        Assert.Null(await cache.GetAsync($"agentcontext:{userId}"));
    }

    [Fact]
    public async Task Send_WhenMessageIsEmpty_ReturnsEmptyContent()
    {
        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            new FakeCacheHandler(),
            new FakeBookContextService(),
            new FakeChatToolRouter(new ChatToolRouteDecision("none", null)),
            "user-1");

        var result = await controller.Send("", CancellationToken.None);

        var content = Assert.IsType<Microsoft.AspNetCore.Mvc.ContentResult>(result);
        Assert.Equal("", content.Content);
    }

    [Fact]
    public async Task Send_WhenNoToolRouted_CallsAgentAndSavesSession()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        var agent = new FakeChatOrchestratorAgent();
        var controller = CreateController(
            agent,
            cache,
            new FakeBookContextService(),
            new FakeChatToolRouter(new ChatToolRouteDecision("none", null)),
            userId);

        var result = await controller.Send("Hello", CancellationToken.None);

        var partial = Assert.IsType<Microsoft.AspNetCore.Mvc.PartialViewResult>(result);
        Assert.Equal("_BotMessage", partial.ViewName);
        Assert.Equal("Hello", agent.LastMessage);
        Assert.DoesNotContain("Working context gathered from tools:", agent.LastInstructions);
        Assert.Equal("""{"session":"updated"}""", await cache.GetAsync($"agentsession:{userId}"));
    }

    [Fact]
    public async Task Send_WhenAgentThrows_ReturnsErrorBotMessage()
    {
        var controller = CreateController(
            new ThrowingChatOrchestratorAgent(),
            new FakeCacheHandler(),
            new FakeBookContextService(),
            new FakeChatToolRouter(new ChatToolRouteDecision("none", null)),
            "user-1");

        var result = await controller.Send("Hello", CancellationToken.None);

        var partial = Assert.IsType<Microsoft.AspNetCore.Mvc.PartialViewResult>(result);
        Assert.Equal("_BotMessage", partial.ViewName);
        Assert.Contains("Error", Assert.IsType<string>(partial.Model));
    }

    private static ChatController CreateController(
        IChatOrchestratorAgent agent,
        ICacheHandler cache,
        IBookContextService toolService,
        IChatToolRouter router,
        string userId)
    {
        var controller = new ChatController(agent, cache, toolService, router, NullLogger<ChatController>.Instance);
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

    private sealed class FakeChatOrchestratorAgent : IChatOrchestratorAgent
    {
        public string? LastMessage { get; private set; }
        public string? LastInstructions { get; private set; }

        public Task<ChatAgentRunResult> RunAsync(string message, string? sessionJson, string? instructions, CancellationToken ct = default)
        {
            LastMessage = message;
            LastInstructions = instructions;
            return Task.FromResult(new ChatAgentRunResult("Grounded answer", """{"session":"updated"}"""));
        }
    }

    private sealed class FakeChatToolRouter(ChatToolRouteDecision decision) : IChatToolRouter
    {
        public Task<ChatToolRouteDecision> RouteAsync(string userId, string message, CancellationToken ct = default)
            => Task.FromResult(decision);
    }

    private sealed class FakeBookContextService : IBookContextService
    {
        public GenerateBookContextToolResult GenerateResult { get; set; } =
            new(Guid.NewGuid(), "Book", "Author", "Context", "Appended");

        public Guid? LastGenerateBookId { get; private set; }

        public Task ClearAsync(Guid bookId, string userId) => Task.CompletedTask;

        public Task<string?> GetContextAsync(Guid bookId, string userId) => Task.FromResult<string?>(null);

        public Task<string> GenerateAndSaveAsync(Guid bookId, string userId, CancellationToken ct = default)
            => Task.FromResult(GenerateResult.GeneratedContext);

        public Task<GenerateBookContextToolResult> GenerateToolResponseAsync(Guid bookId, string userId, string? context, CancellationToken ct = default)
        {
            LastGenerateBookId = bookId;
            return Task.FromResult(GenerateResult);
        }

        public Task<string> SaveManualAsync(Guid bookId, string userId, string context) => Task.FromResult(context);
    }

    private sealed class ThrowingChatOrchestratorAgent : IChatOrchestratorAgent
    {
        public Task<ChatAgentRunResult> RunAsync(string message, string? sessionJson, string? instructions, CancellationToken ct = default)
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
}
