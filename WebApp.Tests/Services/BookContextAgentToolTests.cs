using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class BookContextAgentToolTests
{
    [Fact]
    public async Task Create_WhenBookTitleMatchesUserBook_ReturnsGeneratedContext()
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

        var service = new FakeBookContextService("Arrakis literary context.");
        var tool = new BookContextAgentTool(db, service);
        var function = tool.Create("user-1");

        Assert.Equal("GenerateBookContext", function.Name);

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Dune" },
            CancellationToken.None);

        Assert.Equal("Arrakis literary context.", result?.ToString());
        Assert.True(service.GenerateAndSaveCalled);
    }

    [Fact]
    public async Task Create_WhenBookHasExistingContext_ReturnsCachedContextWithoutGenerating()
    {
        await using var db = CreateDbContext();
        db.Books.Add(new Book
        {
            UserId = "user-1",
            Title = "Foundation",
            Author = "Isaac Asimov",
            NormalizedTitle = "foundation",
            NormalizedAuthor = "isaacasimov",
            Context = "Existing cached context."
        });
        await db.SaveChangesAsync();

        var service = new FakeBookContextService("Should not be called.");
        var tool = new BookContextAgentTool(db, service);
        var function = tool.Create("user-1");

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Foundation" },
            CancellationToken.None);

        Assert.Equal("Existing cached context.", result?.ToString());
        Assert.False(service.GenerateAndSaveCalled);
    }

    [Fact]
    public async Task Create_WhenBookTitleDoesNotMatch_ReturnsNotFoundMessage()
    {
        await using var db = CreateDbContext();
        var service = new FakeBookContextService("context");
        var tool = new BookContextAgentTool(db, service);
        var function = tool.Create("user-1");

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Unknown Book" },
            CancellationToken.None);

        Assert.Contains("not found", result?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(service.GenerateAndSaveCalled);
    }

    [Fact]
    public async Task Create_WhenBookBelongsToOtherUser_ReturnsNotFoundMessage()
    {
        await using var db = CreateDbContext();
        db.Books.Add(new Book
        {
            UserId = "user-2",
            Title = "Dune",
            Author = "Frank Herbert",
            NormalizedTitle = "dune",
            NormalizedAuthor = "frankherbert"
        });
        await db.SaveChangesAsync();

        var service = new FakeBookContextService("context");
        var tool = new BookContextAgentTool(db, service);
        var function = tool.Create("user-1");

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Dune" },
            CancellationToken.None);

        Assert.Contains("not found", result?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(service.GenerateAndSaveCalled);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }

    private sealed class FakeBookContextService(string context) : IBookContextService
    {
        public bool GenerateAndSaveCalled { get; private set; }

        public Task<string?> GetContextAsync(Guid bookId, string userId) => Task.FromResult<string?>(null);

        public Task<string> GenerateAndSaveAsync(Guid bookId, string userId, CancellationToken ct = default)
        {
            GenerateAndSaveCalled = true;
            return Task.FromResult(context);
        }

        public Task<string> SaveManualAsync(Guid bookId, string userId, string ctx) => Task.FromResult(ctx);
        public Task ClearAsync(Guid bookId, string userId) => Task.CompletedTask;
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
