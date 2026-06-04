using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using WebApp.Controllers;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Integration;

public class AgentToolsPostgresTests
{
    [Fact]
    public async Task GenerateBookContext_WithPostgresSeededLibrary_ResolvesTitleWithAuthorAndPersistsContext()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-e2e-1";
        var book = await SeedUserBookAndNoteAsync(db, userId, "Leviathan Wakes", "James S. A. Corey");
        db.BookEmbeddings.Add(CreateBookEmbedding(book, SameVector()));
        await db.SaveChangesAsync();

        var service = new BookContextService(db, new FakeOllamaService("Generated Expanse context."));
        var tool = CreateBookContextTool(db, service, new FakeEmbeddingService(SameVector()), userId);

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Leviathan Wakes by James S. A. Corey" },
            CancellationToken.None);

        Assert.Equal("Generated Expanse context.", result?.ToString());

        var savedBook = await db.Books.SingleAsync(b => b.UserId == userId);
        Assert.Equal("Generated Expanse context.", savedBook.Context);
    }

    [Fact]
    public async Task GenerateBookContext_WithPostgresImportedAuthorDashTitle_ResolvesShortTitleAndPersistsContext()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-e2e-3";
        var book = await SeedUserBookAndNoteAsync(db, userId, "Dick, Philip K - Gather Yourselves Together", "Philip K Dick");
        db.BookEmbeddings.Add(CreateBookEmbedding(book, SameVector()));
        await db.SaveChangesAsync();

        var service = new BookContextService(db, new FakeOllamaService("Generated PKD context."));
        var tool = CreateBookContextTool(db, service, new FakeEmbeddingService(SameVector()), userId);

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Gather Yourselves Together" },
            CancellationToken.None);

        Assert.Equal("Generated PKD context.", result?.ToString());

        var savedBook = await db.Books.SingleAsync(b => b.UserId == userId);
        Assert.Equal("Generated PKD context.", savedBook.Context);
    }

    [Fact]
    public async Task GenerateBookContext_WithPostgresVectorLookup_UsesClosestEmbeddingAndPersistsContext()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-vector-e2e";
        var dune = await SeedUserBookAndNoteAsync(db, userId, "Dune", "Frank Herbert");
        var beach = await SeedUserBookAndNoteAsync(db, userId, "On the Beach", "Nevil Shute");
        db.BookEmbeddings.Add(CreateBookEmbedding(dune, VectorWithFirstValue(1)));
        db.BookEmbeddings.Add(CreateBookEmbedding(beach, VectorWithFirstValue(-1)));
        await db.SaveChangesAsync();

        var service = new BookContextService(db, new FakeOllamaService("Generated On the Beach context."));
        var tool = CreateBookContextTool(db, service, new FakeEmbeddingService(VectorWithFirstValue(-1)), userId);

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "post nuclear australian novel" },
            CancellationToken.None);

        Assert.Equal("Generated On the Beach context.", result?.ToString());

        var duneAfterLookup = await db.Books.SingleAsync(b => b.Id == dune.Id);
        var beachAfterLookup = await db.Books.SingleAsync(b => b.Id == beach.Id);
        Assert.Null(duneAfterLookup.Context);
        Assert.Equal("Generated On the Beach context.", beachAfterLookup.Context);
    }

    [Fact]
    public async Task ChatRefresh_WithPostgresSeededUserAndMafSession_RendersCachedMessages()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-e2e-2";
        var book = await SeedUserBookAndNoteAsync(db, userId, "Galactic Pot-Healer", "Dick, Philip K");
        db.BookEmbeddings.Add(CreateBookEmbedding(book, SameVector()));
        await db.SaveChangesAsync();

        var cache = new FakeCacheHandler();
        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            cache,
            new FakeBookContextAgentTool(),
            db,
            userId);

        await controller.Send("tell me about Dick, Philip K - Galactic Pot-Healer", CancellationToken.None);
        var result = await controller.Chat(CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        var entries = Assert.IsAssignableFrom<List<ChatEntry>>(partial.Model);
        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal("user", entry.Role);
                Assert.Equal("tell me about Dick, Philip K - Galactic Pot-Healer", entry.Content);
            },
            entry =>
            {
                Assert.Equal("assistant", entry.Role);
                Assert.Contains("Galactic Pot-Healer context", entry.Content);
            });
    }

    private const string MafSessionJson =
        """
        {
          "stateBag": {
            "InMemoryChatHistoryProvider": {
              "messages": [
                {
                  "role": "user",
                  "contents": [
                    { "$type": "text", "text": "tell me about Dick, Philip K - Galactic Pot-Healer" }
                  ]
                },
                {
                  "authorName": "LocalOllamaAgent",
                  "createdAt": "2026-05-10T22:08:54.0064265+00:00",
                  "role": "assistant",
                  "contents": [
                    { "$type": "functionCall", "name": "GenerateBookContext", "arguments": { "bookTitle": "Dick, Philip K - Galactic Pot-Healer" }, "informationalOnly": true, "callId": "call-1" }
                  ]
                },
                {
                  "authorName": "LocalOllamaAgent",
                  "role": "tool",
                  "contents": [
                    { "$type": "functionResult", "result": "Galactic Pot-Healer context.", "callId": "call-1" }
                  ]
                },
                {
                  "authorName": "LocalOllamaAgent",
                  "createdAt": "2026-05-10T22:08:55.0064265+00:00",
                  "role": "assistant",
                  "contents": [
                    { "$type": "text", "text": "Galactic Pot-Healer context is ready." }
                  ]
                }
              ]
            }
          }
        }
        """;

    private static ChatController CreateController(
        IChatOrchestratorAgent agent,
        ICacheHandler cache,
        IBookContextAgentTool bookContextTool,
        AppDbContext db,
        string userId)
    {
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

    private static async Task<Book> SeedUserBookAndNoteAsync(AppDbContext db, string userId, string title, string author)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            db.Users.Add(new IdentityUser
            {
                Id = userId,
                UserName = $"{userId}@example.test",
                NormalizedUserName = $"{userId}@EXAMPLE.TEST",
                Email = $"{userId}@example.test",
                NormalizedEmail = $"{userId}@EXAMPLE.TEST"
            });
        }

        if (!await db.UserProfiles.AnyAsync(p => p.UserId == userId))
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Nickname = "Reader",
                PreferredLanguage = "English",
                AgentProfileCompact = "{}"
            });
        }

        var book = new Book
        {
            UserId = userId,
            Title = title,
            Author = author,
            NormalizedTitle = NormalizeKey(title),
            NormalizedAuthor = NormalizeKey(author)
        };

        db.Books.Add(book);
        db.BookNotes.Add(new BookNote
        {
            UserId = userId,
            BookId = book.Id,
            EntryType = "Highlight",
            LocationText = "Location 42",
            Content = "A seeded note for the integration test.",
            ClippedAtUtc = DateTime.UtcNow,
            DedupeKey = Guid.NewGuid().ToString("N")
        });
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

    private static AIFunction CreateBookContextTool(
        AppDbContext db,
        IBookContextService bookContextService,
        IEmbeddingService embeddingService,
        string userId)
    {
        var lookup = new BookLookupService(db, embeddingService, NullLogger<BookLookupService>.Instance);
        return new BookContextAgentTool(bookContextService, lookup).Create(userId);
    }

    private static float[] SameVector()
    {
        var vector = new float[1024];
        vector[0] = 1;
        return vector;
    }

    private static float[] VectorWithFirstValue(float value)
    {
        var vector = new float[1024];
        vector[0] = value;
        return vector;
    }

    private static string NormalizeKey(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private sealed class FakeOllamaService(string response) : IOllamaService
    {
        public Task<string> CompleteAsync(string prompt, CancellationToken ct = default) =>
            Task.FromResult(response);
    }

    private sealed class FakeEmbeddingService(float[] vector) : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(vector);
    }

    private sealed class FakeChatOrchestratorAgent : IChatOrchestratorAgent
    {
        public Task<ChatAgentRunResult> RunAsync(string message, string? sessionJson, string? instructions, IReadOnlyList<AITool>? tools = null, CancellationToken ct = default) =>
            Task.FromResult(new ChatAgentRunResult("Saved answer", AgentToolsPostgresTests.MafSessionJson));
    }

    private sealed class FakeBookContextAgentTool : IBookContextAgentTool
    {
        public AIFunction Create(string userId) =>
            AIFunctionFactory.Create((string bookTitle) => Task.FromResult("context"), name: "GenerateBookContext");
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
