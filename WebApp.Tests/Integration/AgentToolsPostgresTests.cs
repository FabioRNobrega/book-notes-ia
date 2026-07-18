using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
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

        var service = new BookContextService(
            db,
            new FakeChatCompletionService("Generated Expanse context."),
            new FakeOpenLibraryService());
        var tool = CreateBookContextTool(db, service, new FakeEmbeddingService(SameVector()), userId);

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Leviathan Wakes by James S. A. Corey" },
            CancellationToken.None);

        Assert.Equal("<book-context>\nGenerated Expanse context.\n</book-context>", result?.ToString());

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

        var service = new BookContextService(
            db,
            new FakeChatCompletionService("Generated PKD context."),
            new FakeOpenLibraryService());
        var tool = CreateBookContextTool(db, service, new FakeEmbeddingService(SameVector()), userId);

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Gather Yourselves Together" },
            CancellationToken.None);

        Assert.Equal("<book-context>\nGenerated PKD context.\n</book-context>", result?.ToString());

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

        var service = new BookContextService(
            db,
            new FakeChatCompletionService("Generated On the Beach context."),
            new FakeOpenLibraryService());
        var tool = CreateBookContextTool(db, service, new FakeEmbeddingService(VectorWithFirstValue(-1)), userId);

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "post nuclear australian novel" },
            CancellationToken.None);

        Assert.Equal("<book-context>\nGenerated On the Beach context.\n</book-context>", result?.ToString());

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
            new FakeBookNotesAgentTool(),
            db,
            userId);

        await controller.Send(new ChatSendRequest { Message = "tell me about Dick, Philip K - Galactic Pot-Healer" }, CancellationToken.None);
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
                Assert.Contains("Saved answer", entry.Content);
            });
    }

    [Fact]
    public async Task ChatSend_WritesTwoChatMessageRows()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-chat-write-e2e";
        await SeedUserAndBookAsync(db, userId, "Dune", "Frank Herbert");

        var cache = new FakeCacheHandler();
        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            cache,
            new FakeBookContextAgentTool(),
            new FakeBookNotesAgentTool(),
            db,
            userId);

        await controller.Send(new ChatSendRequest { Message = "Tell me about Dune" }, CancellationToken.None);

        var messages = await db.ChatMessages
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        Assert.Collection(
            messages,
            userMessage =>
            {
                Assert.Equal("user", userMessage.Role);
                Assert.Equal("Tell me about Dune", userMessage.Content);
                Assert.Null(userMessage.TotalInputTokensProcessed);
            },
            assistantMessage =>
            {
                Assert.Equal("assistant", assistantMessage.Role);
                Assert.Equal("Saved answer", assistantMessage.Content);
                Assert.Equal(500, assistantMessage.TotalInputTokensProcessed);
                Assert.Equal(200, assistantMessage.TotalOutputTokensGenerated);
                Assert.Equal(300, assistantMessage.LatestPromptTokens);
                Assert.Equal(500, assistantMessage.MaxPromptTokens);
                Assert.Equal(2, assistantMessage.ModelCallCount);
                Assert.Equal(38000, assistantMessage.ResponseTimeMs);
                Assert.Equal("free", assistantMessage.AgentType);
            });
    }

    [Fact]
    public async Task ChatReset_ClearsSessionId_NewSessionHasNoMessages()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-chat-reset-e2e";
        await SeedUserAndBookAsync(db, userId, "Dune", "Frank Herbert");

        var cache = new FakeCacheHandler();
        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            cache,
            new FakeBookContextAgentTool(),
            new FakeBookNotesAgentTool(),
            db,
            userId);

        await controller.Send(new ChatSendRequest { Message = "Tell me about Dune" }, CancellationToken.None);
        var oldSessionId = await cache.GetAsync($"activesessionid:{userId}");

        await controller.Reset(CancellationToken.None);
        var result = await controller.Chat(CancellationToken.None);

        Assert.NotNull(oldSessionId);
        Assert.Null(await cache.GetAsync($"activesessionid:{userId}"));
        Assert.Null(await cache.GetAsync($"agentsession:{userId}:{oldSessionId}"));
        Assert.Null(await cache.GetAsync($"agentcontext:{userId}:{oldSessionId}"));
        Assert.Empty(await db.ChatMessages.Where(x => x.UserId == userId).ToListAsync());
        var partial = Assert.IsType<PartialViewResult>(result);
        var entries = Assert.IsAssignableFrom<List<ChatEntry>>(partial.Model);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task ChatController_Chat_ReadsFromDb_NotSessionJson()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-chat-db-read-e2e";
        await SeedUserAndBookAsync(db, userId, "Dune", "Frank Herbert");
        var sessionId = Guid.NewGuid();
        db.ChatMessages.AddRange(
            new WebApp.Models.ChatMessage
            {
                UserId = userId,
                SessionId = sessionId,
                Role = "user",
                Content = "DB user message",
                DisplayOrder = 1
            },
            new WebApp.Models.ChatMessage
            {
                UserId = userId,
                SessionId = sessionId,
                Role = "assistant",
                Content = "DB assistant message",
                DisplayOrder = 2
            });
        await db.SaveChangesAsync();

        var cache = new FakeCacheHandler();
        await cache.SetAsync($"activesessionid:{userId}", sessionId.ToString("D"), TimeSpan.FromMinutes(5));
        await cache.SetAsync($"agentsession:{userId}:{sessionId:D}", MafSessionJson, TimeSpan.FromMinutes(5));
        var controller = CreateController(
            new FakeChatOrchestratorAgent(),
            cache,
            new FakeBookContextAgentTool(),
            new FakeBookNotesAgentTool(),
            db,
            userId);

        var result = await controller.Chat(CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        var entries = Assert.IsAssignableFrom<List<ChatEntry>>(partial.Model);
        Assert.Collection(
            entries,
            entry => Assert.Equal("DB user message", entry.Content),
            entry => Assert.Contains("DB assistant message", entry.Content));
    }

    [Fact]
    public async Task GetBookNotesWithAnalysis_WithSeededNotes_ReturnsFormattedNoteBlocks()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-notes-e2e-1";
        var book = await SeedUserAndBookAsync(db, userId, "Dune", "Frank Herbert", "Arrakis context.");
        AddBookNote(db, book, "The mystery of life isn't a problem to solve.", DateTime.UtcNow.AddMinutes(-2));
        AddBookNote(db, book, "Fear is the mind-killer.", DateTime.UtcNow.AddMinutes(-1));
        await db.SaveChangesAsync();

        var function = CreateBookNotesTool(db, MakeAnalysisService(db), new FakeEmbeddingService(SameVector()), userId);

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Dune" },
            CancellationToken.None);

        var text = result?.ToString();
        Assert.Contains("Notes for \"Dune\"", text);
        Assert.Contains("2 highlights", text);
        Assert.Contains("<note>The mystery of life isn't a problem to solve.</note>", text);
        Assert.Contains("<note>Fear is the mind-killer.</note>", text);
    }

    [Fact]
    public async Task GetBookNotesWithAnalysis_WithNoNotes_ReturnsNoNotesMessage()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-notes-e2e-2";
        await SeedUserAndBookAsync(db, userId, "Dune", "Frank Herbert");

        var function = CreateBookNotesTool(db, MakeAnalysisService(db), new FakeEmbeddingService(SameVector()), userId);

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Dune" },
            CancellationToken.None);

        Assert.Contains("No notes or highlights found", result?.ToString());
    }

    [Fact]
    public async Task GetBookNotesWithAnalysis_WithUnknownTitle_ReturnsNotFoundMessage()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-notes-e2e-3";
        await SeedUserAndBookAsync(db, userId, "Dune", "Frank Herbert");

        var analysis = new FakeBookNotesAnalysisService("Should not be called.");
        var function = CreateBookNotesTool(db, analysis, new FakeEmbeddingService(SameVector()), userId);

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Unknown Title" },
            CancellationToken.None);

        Assert.Contains("was not found in your library", result?.ToString());
        Assert.False(analysis.Called);
    }

    [Fact]
    public async Task GetBookNotesWithAnalysis_IsolatesNotesByUserId_DoesNotReturnOtherUserNotes()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userA = "user-notes-e2e-4a";
        var userB = "user-notes-e2e-4b";
        var bookA = await SeedUserAndBookAsync(db, userA, "Dune", "Frank Herbert");
        await SeedUserAndBookAsync(db, userB, "Dune", "Frank Herbert");
        AddBookNote(db, bookA, "Other user's private highlight.", DateTime.UtcNow);
        await db.SaveChangesAsync();

        var function = CreateBookNotesTool(db, MakeAnalysisService(db), new FakeEmbeddingService(SameVector()), userB);

        var result = await function.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Dune" },
            CancellationToken.None);

        Assert.Contains("No notes or highlights found", result?.ToString());
        Assert.DoesNotContain("Other user's private highlight.", result?.ToString());
    }

    [Fact]
    public async Task GetRelevantBookNotes_WithSeededEmbeddings_ReturnsClosestNoteWithLocAttribute()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-search-e2e-1";
        var book = await SeedUserAndBookAsync(db, userId, "Dune", "Frank Herbert");
        var noteA = AddBookNoteReturned(db, book, "Paul feels the presence of water within the desert.", "Location 10", DateTime.UtcNow.AddMinutes(-2));
        var noteB = AddBookNoteReturned(db, book, "Fremen religion is shaped by the ecology of Arrakis.", "Location 200", DateTime.UtcNow.AddMinutes(-1));
        await db.SaveChangesAsync();

        db.BookNoteEmbeddings.Add(CreateBookNoteEmbedding(book, noteA, VectorWithFirstValue(1)));
        db.BookNoteEmbeddings.Add(CreateBookNoteEmbedding(book, noteB, VectorWithFirstValue(-1)));
        await db.SaveChangesAsync();

        var tool = CreateBookNoteSearchTool(db, new FakeEmbeddingService(VectorWithFirstValue(1)), userId);

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Dune", ["searchQuery"] = "water and desert" },
            CancellationToken.None);

        var text = result?.ToString();
        Assert.Contains("Location 10", text);
        Assert.Contains("Paul feels the presence of water within the desert.", text);
        Assert.Contains("<note loc=", text);
    }

    [Fact]
    public async Task GetRelevantBookNotes_WithUnknownTitle_ReturnsNotFoundMessage()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-search-e2e-2";
        await SeedUserAndBookAsync(db, userId, "Dune", "Frank Herbert");

        var tool = CreateBookNoteSearchTool(db, new FakeEmbeddingService(SameVector()), userId);

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Unknown Title", ["searchQuery"] = "anything" },
            CancellationToken.None);

        Assert.Contains("was not found in your library", result?.ToString());
    }

    [Fact]
    public async Task GetRelevantBookNotes_WithNoMatchingEmbeddings_ReturnsNoRelevantNotesMessage()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-search-e2e-3";
        await SeedUserAndBookAsync(db, userId, "Dune", "Frank Herbert");

        var tool = CreateBookNoteSearchTool(db, new FakeEmbeddingService(SameVector()), userId);

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Dune", ["searchQuery"] = "ecology" },
            CancellationToken.None);

        Assert.Contains("No relevant notes found", result?.ToString());
    }

    [Fact]
    public async Task GetRelevantBookNotes_IsolatesEmbeddingsByUserId_DoesNotReturnOtherUserNotes()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userA = "user-search-e2e-4a";
        var userB = "user-search-e2e-4b";
        var bookA = await SeedUserAndBookAsync(db, userA, "Dune", "Frank Herbert");
        await SeedUserAndBookAsync(db, userB, "Dune", "Frank Herbert");

        var noteA = AddBookNoteReturned(db, bookA, "User A private highlight.", "Location 1", DateTime.UtcNow);
        await db.SaveChangesAsync();
        db.BookNoteEmbeddings.Add(CreateBookNoteEmbedding(bookA, noteA, VectorWithFirstValue(1)));
        await db.SaveChangesAsync();

        // Search scoped to userB — who has no embeddings
        var tool = CreateBookNoteSearchTool(db, new FakeEmbeddingService(VectorWithFirstValue(1)), userB);

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["bookTitle"] = "Dune", ["searchQuery"] = "private" },
            CancellationToken.None);

        Assert.Contains("No relevant notes found", result?.ToString());
        Assert.DoesNotContain("User A private highlight.", result?.ToString());
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
        IBookNotesAgentTool bookNotesTool,
        AppDbContext db,
        string userId,
        IBookNoteSearchAgentTool? bookNoteSearchTool = null)
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var controller = new ChatController(agent, new FakeChatAgentProvider(), cache, bookContextTool, bookNotesTool, bookNoteSearchTool ?? new FakeBookNoteSearchAgentTool(), db, new FakeChatMessageAudioService(), NullLogger<ChatController>.Instance, configuration);
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
        var book = await SeedUserAndBookAsync(db, userId, title, author);
        AddBookNote(db, book, "A seeded note for the integration test.", DateTime.UtcNow);
        await db.SaveChangesAsync();
        return book;
    }

    private static async Task<Book> SeedUserAndBookAsync(
        AppDbContext db,
        string userId,
        string title,
        string author,
        string? context = null,
        string preferredLanguage = "English")
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
                PreferredLanguage = preferredLanguage,
                AgentProfileCompact = "{}"
            });
        }

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

    private static void AddBookNote(AppDbContext db, Book book, string content, DateTime clippedAtUtc)
    {
        db.BookNotes.Add(new BookNote
        {
            UserId = book.UserId,
            BookId = book.Id,
            EntryType = "Highlight",
            LocationText = "Location 42",
            Content = content,
            ClippedAtUtc = clippedAtUtc,
            DedupeKey = Guid.NewGuid().ToString("N")
        });
    }

    private static BookNote AddBookNoteReturned(AppDbContext db, Book book, string content, string locationText, DateTime clippedAtUtc)
    {
        var note = new BookNote
        {
            UserId = book.UserId,
            BookId = book.Id,
            EntryType = "Highlight",
            LocationText = locationText,
            Content = content,
            ClippedAtUtc = clippedAtUtc,
            DedupeKey = Guid.NewGuid().ToString("N")
        };
        db.BookNotes.Add(note);
        return note;
    }

    private static BookNoteEmbedding CreateBookNoteEmbedding(Book book, BookNote note, float[] vector) =>
        new()
        {
            UserId = book.UserId,
            BookId = book.Id,
            BookNoteId = note.Id,
            Embedding = new Vector(vector)
        };

    private static AIFunction CreateBookNoteSearchTool(
        AppDbContext db,
        IEmbeddingService embeddingService,
        string userId)
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var lookup = new BookLookupService(db, embeddingService, NullLogger<BookLookupService>.Instance);
        var searchService = new BookNoteSearchService(db, embeddingService, config);
        return new BookNoteSearchAgentTool(lookup, searchService).Create(userId);
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
        return new BookContextAgentTool(bookContextService, lookup).Create(userId, "free");
    }

    private static BookNotesAnalysisService MakeAnalysisService(AppDbContext db) =>
        new(db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

    private static AIFunction CreateBookNotesTool(
        AppDbContext db,
        IBookNotesAnalysisService bookNotesAnalysisService,
        IEmbeddingService embeddingService,
        string userId)
    {
        var lookup = new BookLookupService(db, embeddingService, NullLogger<BookLookupService>.Instance);
        return new BookNotesAgentTool(lookup, bookNotesAnalysisService).Create(userId);
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

    private sealed class FakeChatCompletionService(string response) : IChatCompletionService
    {
        public int CallCount { get; private set; }
        public string LastPrompt { get; private set; } = string.Empty;
        public string LastAgentKey { get; private set; } = string.Empty;

        public Task<string> CompleteAsync(string prompt, string agentKey, CancellationToken ct = default)
        {
            CallCount++;
            LastPrompt = prompt;
            LastAgentKey = agentKey;
            return Task.FromResult(response);
        }
    }

    private sealed class FakeEmbeddingService(float[] vector) : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(vector);
    }

    private sealed class FakeOpenLibraryService : IOpenLibraryService
    {
        public Task<string?> GetSynopsisAsync(string title, string author, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FakeChatOrchestratorAgent : IChatOrchestratorAgent
    {
        public Task<ChatAgentRunResult> RunAsync(AIAgent agent, string message, string? sessionJson, string? instructions, IReadOnlyList<AITool>? tools = null, CancellationToken ct = default) =>
            Task.FromResult(new ChatAgentRunResult("Saved answer", AgentToolsPostgresTests.MafSessionJson, 500, 200, 300, 150, 500, 200, 2, 38000));
    }

    private sealed class FakeChatAgentProvider : IChatAgentProvider
    {
        private static readonly AIAgent Agent = new ChatClientAgent(new FakeChatClient(), name: "TestAgent");

        public AIAgent GetAgent(string agentKey) => Agent;
    }

    private sealed class FakeBookContextAgentTool : IBookContextAgentTool
    {
        public AIFunction Create(string userId, string agentKey) =>
            AIFunctionFactory.Create((string bookTitle) => Task.FromResult("context"), name: "GenerateBookContext");
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

    private sealed class FakeBookNotesAgentTool : IBookNotesAgentTool
    {
        public AIFunction Create(string userId) =>
            AIFunctionFactory.Create((string bookTitle) => Task.FromResult("notes"), name: "GetBookNotesWithAnalysis");
    }

    private sealed class FakeBookNoteSearchAgentTool : IBookNoteSearchAgentTool
    {
        public AIFunction Create(string userId) =>
            AIFunctionFactory.Create((string bookTitle, string searchQuery) => Task.FromResult("relevant notes"), name: "GetRelevantBookNotes");
    }

    private sealed class FakeBookNotesAnalysisService(string result) : IBookNotesAnalysisService
    {
        public bool Called { get; private set; }

        public Task<string> GetNotesAsync(Book book, string userId, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(result);
        }
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

    private sealed class FakeChatMessageAudioService : WebApp.Services.IChatMessageAudioService
    {
        public Task<(byte[] WavBytes, string ContentType)?> GetOrCreateAudioAsync(string userId, Guid messageId, CancellationToken ct = default)
            => Task.FromResult<(byte[], string)?>(null);
    }
}
