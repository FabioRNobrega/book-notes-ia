using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using WebApp.Models;
using WebApp.Services;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Services;

public class BookContextAgentToolTests
{
    [Fact]
    public async Task Create_WhenEmbeddingMatchesUserBook_ReturnsGeneratedContext()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var book = await SeedUserAndBookAsync(db, "user-1", "Dune", "Frank Herbert");
        db.BookEmbeddings.Add(CreateBookEmbedding(book, VectorA()));
        await db.SaveChangesAsync();

        var service = new FakeBookContextService("Arrakis literary context.");
        var tool = CreateTool(db, service, new FakeEmbeddingService(VectorA()));
        var function = tool.Create("user-1");

        Assert.Equal("GenerateBookContext", function.Name);

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "desert planet messiah novel" },
            CancellationToken.None);

        Assert.Equal("Arrakis literary context.", result?.ToString());
        Assert.True(service.GenerateAndSaveCalled);
    }

    [Fact]
    public async Task Create_WhenBookBelongsToOtherUser_ReturnsNotFoundOrFallback()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var book = await SeedUserAndBookAsync(db, "user-2", "Dune", "Frank Herbert");
        db.BookEmbeddings.Add(CreateBookEmbedding(book, VectorA()));
        await db.SaveChangesAsync();

        var service = new FakeBookContextService("context");
        var tool = CreateTool(db, service, new FakeEmbeddingService(VectorA()));
        var function = tool.Create("user-1");

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Dune" },
            CancellationToken.None);

        Assert.Contains("not found", result?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(service.GenerateAndSaveCalled);
    }

    [Fact]
    public async Task Create_WhenNoEmbeddingExistsAndStringMatchFails_ReturnsNotFound()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        await SeedUserAndBookAsync(db, "user-1", "Dune", "Frank Herbert");

        var service = new FakeBookContextService("context");
        var tool = CreateTool(db, service, new FakeEmbeddingService(VectorA()));
        var function = tool.Create("user-1");

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Unknown Book" },
            CancellationToken.None);

        Assert.Contains("not found", result?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(service.GenerateAndSaveCalled);
    }

    [Fact]
    public async Task Create_WhenNoEmbeddingExistsButStringMatchSucceeds_ReturnsContext()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        await SeedUserAndBookAsync(db, "user-1", "Dick, Philip K - Gather Yourselves Together", "Philip K Dick");

        var service = new FakeBookContextService("Gather Yourselves Together context.");
        var tool = CreateTool(db, service, new FakeEmbeddingService(VectorA()));
        var function = tool.Create("user-1");

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Gather Yourselves Together" },
            CancellationToken.None);

        Assert.Equal("Gather Yourselves Together context.", result?.ToString());
        Assert.True(service.GenerateAndSaveCalled);
    }

    [Fact]
    public async Task Create_WhenBookHasExistingContext_ReturnsCachedContextWithoutGenerating()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var book = await SeedUserAndBookAsync(db, "user-1", "Foundation", "Isaac Asimov", "Existing cached context.");
        db.BookEmbeddings.Add(CreateBookEmbedding(book, VectorA()));
        await db.SaveChangesAsync();

        var service = new FakeBookContextService("Should not be called.");
        var tool = CreateTool(db, service, new FakeEmbeddingService(VectorA()));
        var function = tool.Create("user-1");

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Foundation" },
            CancellationToken.None);

        Assert.Equal("Existing cached context.", result?.ToString());
        Assert.False(service.GenerateAndSaveCalled);
    }

    [Fact]
    public async Task Create_WhenEmbeddingFailsAndStringMatchSucceeds_ReturnsContext()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        await SeedUserAndBookAsync(db, "user-1", "Dune", "Frank Herbert");

        var service = new FakeBookContextService("Fallback Dune context.");
        var tool = CreateTool(db, service, new ThrowingEmbeddingService());
        var function = tool.Create("user-1");

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Dune" },
            CancellationToken.None);

        Assert.Equal("Fallback Dune context.", result?.ToString());
        Assert.True(service.GenerateAndSaveCalled);
    }

    private static BookContextAgentTool CreateTool(
        AppDbContext db,
        IBookContextService bookContextService,
        IEmbeddingService embeddingService)
    {
        var lookup = new BookLookupService(db, embeddingService, NullLogger<BookLookupService>.Instance);
        return new BookContextAgentTool(bookContextService, lookup);
    }

    private static async Task<Book> SeedUserAndBookAsync(
        AppDbContext db,
        string userId,
        string title,
        string author,
        string? context = null)
    {
        db.Users.Add(new IdentityUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            NormalizedUserName = $"{userId}@EXAMPLE.TEST",
            Email = $"{userId}@example.test",
            NormalizedEmail = $"{userId}@EXAMPLE.TEST"
        });

        var book = new Book
        {
            UserId = userId,
            Title = title,
            SourceBookTitle = title,
            Author = author,
            NormalizedTitle = NormalizeKey(title),
            NormalizedAuthor = NormalizeKey(author),
            Context = context
        };

        db.Books.Add(book);
        await db.SaveChangesAsync();
        return book;
    }

    private static BookEmbedding CreateBookEmbedding(Book book, float[] vector) =>
        new()
        {
            UserId = book.UserId,
            BookId = book.Id,
            Title = book.Title,
            Author = book.Author,
            Embedding = new Vector(vector)
        };

    private static string NormalizeKey(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static float[] VectorA()
    {
        var vector = new float[1024];
        vector[0] = 1;
        return vector;
    }

    private sealed class FakeEmbeddingService(float[] vector) : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(vector);
    }

    private sealed class ThrowingEmbeddingService : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            throw new InvalidOperationException("Embedding endpoint unavailable.");
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
}
