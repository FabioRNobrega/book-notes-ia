using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApp.Controllers;
using WebApp.Services;

namespace WebApp.Tests.Controllers;

public class BookContextControllerTests
{
    [Fact]
    public async Task Generate_ReturnsContextWhenBookExists()
    {
        var bookId = Guid.NewGuid();
        var service = new FakeBookContextService { GenerateAndSaveResult = "Fresh literary context." };
        var controller = CreateController(service, "user-1");

        var result = await controller.Generate(bookId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Fresh literary context.", json);
    }

    [Fact]
    public async Task Generate_ReturnsNotFoundWhenBookDoesNotExist()
    {
        var controller = CreateController(new FakeBookContextService
        {
            GenerateException = new KeyNotFoundException("missing book")
        }, "user-1");

        var result = await controller.Generate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Generate_UsesSelectedAgentFromCache_NotHardcodedFree()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        await cache.SetAsync($"activeagent:{userId}", "free-llama3", TimeSpan.FromMinutes(5));
        var service = new FakeBookContextService { GenerateAndSaveResult = "Fresh literary context." };
        var controller = CreateController(service, userId, cache);

        await controller.Generate(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("free-llama3", service.LastAgentKey);
    }

    [Fact]
    public async Task Generate_WhenNoActiveAgentCached_DefaultsToFreeQwen()
    {
        var userId = "user-1";
        var service = new FakeBookContextService { GenerateAndSaveResult = "Fresh literary context." };
        var controller = CreateController(service, userId, new FakeCacheHandler());

        await controller.Generate(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("free-qwen", service.LastAgentKey);
    }

    private static BookContextController CreateController(IBookContextService service, string userId, ICacheHandler? cache = null)
    {
        var controller = new BookContextController(service, cache ?? new FakeCacheHandler());
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

    private sealed class FakeBookContextService : IBookContextService
    {
        public string GenerateAndSaveResult { get; set; } = "Context.";
        public Exception? GenerateException { get; set; }
        public string? LastAgentKey { get; private set; }

        public Task ClearAsync(Guid bookId, string userId) => Task.CompletedTask;

        public Task<string?> GetContextAsync(Guid bookId, string userId) => Task.FromResult<string?>(null);

        public Task<string> GenerateAndSaveAsync(Guid bookId, string userId, string agentKey, CancellationToken ct = default)
        {
            LastAgentKey = agentKey;
            if (GenerateException is not null)
                throw GenerateException;
            return Task.FromResult(GenerateAndSaveResult);
        }

        public Task<string> SaveManualAsync(Guid bookId, string userId, string context) => Task.FromResult(context);
    }

    private sealed class FakeCacheHandler : ICacheHandler
    {
        private readonly Dictionary<string, string> _store = [];

        public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);

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
