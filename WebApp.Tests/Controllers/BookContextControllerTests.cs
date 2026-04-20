using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApp.Controllers;
using WebApp.Services;

namespace WebApp.Tests.Controllers;

public class BookContextControllerTests
{
    [Fact]
    public async Task Generate_ReturnsToolPayloadWithAppendedContext()
    {
        var bookId = Guid.NewGuid();
        var service = new FakeBookContextService
        {
            GenerateResult = new GenerateBookContextToolResult(
                bookId,
                "Dune",
                "Frank Herbert",
                "Fresh summary",
                "Existing context\n\n[GenerateBookContext]\nBook: Dune")
        };
        var controller = CreateController(service, "user-1");

        var result = await controller.Generate(bookId, new GenerateBookContextToolRequest("Existing context"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<GenerateBookContextToolResponse>(ok.Value);
        Assert.Equal("GenerateBookContext", payload.ToolName);
        Assert.Equal(bookId, payload.BookId);
        Assert.Equal("Fresh summary", payload.GeneratedContext);
        Assert.Contains("Existing context", payload.AppendedContext);
        Assert.Equal("Existing context", service.LastGenerateContext);
    }

    [Fact]
    public async Task Generate_ReturnsNotFoundWhenBookDoesNotExist()
    {
        var controller = CreateController(new FakeBookContextService
        {
            GenerateException = new KeyNotFoundException("missing book")
        }, "user-1");

        var result = await controller.Generate(Guid.NewGuid(), new GenerateBookContextToolRequest(null), CancellationToken.None);

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
        public GenerateBookContextToolResult GenerateResult { get; set; } =
            new(Guid.NewGuid(), "Book", "Author", "Context", "Appended");

        public Exception? GenerateException { get; set; }
        public string? LastGenerateContext { get; private set; }

        public Task ClearAsync(Guid bookId, string userId) => Task.CompletedTask;

        public Task<string?> GetContextAsync(Guid bookId, string userId) => Task.FromResult<string?>(null);

        public Task<string> GenerateAndSaveAsync(Guid bookId, string userId, CancellationToken ct = default)
            => Task.FromResult(GenerateResult.GeneratedContext);

        public Task<GenerateBookContextToolResult> GenerateToolResponseAsync(Guid bookId, string userId, string? context, CancellationToken ct = default)
        {
            if (GenerateException is not null)
                throw GenerateException;

            LastGenerateContext = context;
            return Task.FromResult(GenerateResult);
        }

        public Task<string> SaveManualAsync(Guid bookId, string userId, string context) => Task.FromResult(context);
    }
}
