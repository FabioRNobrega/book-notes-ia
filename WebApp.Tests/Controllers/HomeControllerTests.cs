using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Controllers;
using WebApp.Services;

namespace WebApp.Tests.Controllers;

public class HomeControllerTests
{
    [Fact]
    public async Task Index_WhenActiveAgentCached_SetsViewData()
    {
        var userId = "user-1";
        var cache = new FakeCacheHandler();
        await cache.SetAsync($"activeagent:{userId}", "premium", TimeSpan.FromMinutes(5));
        var controller = CreateController(cache, userId);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        Assert.Equal("premium", controller.ViewData["ActiveAgent"]);
    }

    [Fact]
    public async Task Index_WhenActiveAgentMissing_DefaultsToFree()
    {
        var controller = CreateController(new FakeCacheHandler(), "user-1");

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        Assert.Equal("free", controller.ViewData["ActiveAgent"]);
    }

    private static HomeController CreateController(ICacheHandler cache, string userId)
    {
        var controller = new HomeController(
            NullLogger<HomeController>.Instance,
            new FakeUnsplashService(),
            cache);
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

    private sealed class FakeUnsplashService : IUnsplashService
    {
        public Task<UnsplashPhoto?> GetBookPhotoAsync() => Task.FromResult<UnsplashPhoto?>(null);
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
