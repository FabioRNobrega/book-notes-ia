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

    private static BookContextController CreateController(IBookContextService service, string userId)
    {
        var controller = new BookContextController(service);
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

        public Task ClearAsync(Guid bookId, string userId) => Task.CompletedTask;

        public Task<string?> GetContextAsync(Guid bookId, string userId) => Task.FromResult<string?>(null);

        public Task<string> GenerateAndSaveAsync(Guid bookId, string userId, CancellationToken ct = default)
        {
            if (GenerateException is not null)
                throw GenerateException;
            return Task.FromResult(GenerateAndSaveResult);
        }

        public Task<string> SaveManualAsync(Guid bookId, string userId, string context) => Task.FromResult(context);
    }
}
