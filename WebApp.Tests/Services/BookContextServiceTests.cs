using Microsoft.EntityFrameworkCore;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class BookContextServiceTests
{
    [Fact]
    public async Task GenerateAndSaveAsync_PersistsGeneratedContextToBook()
    {
        await using var db = CreateDbContext();
        var userId = "user-1";
        var book = new Book
        {
            UserId = userId,
            Title = "The Left Hand of Darkness",
            Author = "Ursula K. Le Guin",
            NormalizedTitle = "thelefthandofdarkness",
            NormalizedAuthor = "ursulakleguin"
        };

        db.Books.Add(book);
        db.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            Nickname = "Reader",
            PreferredLanguage = "Portuguese",
            AgentProfileCompact = "{}"
        });
        await db.SaveChangesAsync();

        var ollama = new FakeOllamaService("Generated summary from Ollama.");
        var openLibrary = new FakeOpenLibraryService(null);
        var service = new BookContextService(db, ollama, openLibrary);

        var result = await service.GenerateAndSaveAsync(book.Id, userId, CancellationToken.None);

        Assert.Equal("Generated summary from Ollama.", result);

        var savedBook = await db.Books.SingleAsync();
        Assert.Equal("Generated summary from Ollama.", savedBook.Context);
        Assert.Null(savedBook.Synopsis);
        Assert.Equal(1, openLibrary.CallCount);
        Assert.Contains("Respond in Portuguese.", ollama.LastPrompt);
    }

    [Fact]
    public async Task GenerateAndSaveAsync_WithSynopsis_IncludesSynopsisInOllamaPrompt()
    {
        await using var db = CreateDbContext();
        var userId = "user-1";
        var book = SeedBook(db, userId, "Dune", "Frank Herbert");
        await db.SaveChangesAsync();

        var synopsis = "A desert world novel about ecology, power, and prophecy.";
        var ollama = new FakeOllamaService("Generated Dune context.");
        var openLibrary = new FakeOpenLibraryService(synopsis);
        var service = new BookContextService(db, ollama, openLibrary);

        var result = await service.GenerateAndSaveAsync(book.Id, userId, CancellationToken.None);

        Assert.Equal("Generated Dune context.", result);
        Assert.Contains(synopsis, ollama.LastPrompt);
        Assert.Equal(1, ollama.CallCount);
        Assert.Equal(1, openLibrary.CallCount);

        var savedBook = await db.Books.SingleAsync();
        Assert.Equal(synopsis, savedBook.Synopsis);
        Assert.Equal("Generated Dune context.", savedBook.Context);
    }

    [Fact]
    public async Task GenerateAndSaveAsync_WithExistingSynopsis_DoesNotCallOpenLibrary()
    {
        await using var db = CreateDbContext();
        var userId = "user-1";
        var existingSynopsis = "Stored synopsis from a previous generation.";
        var book = SeedBook(db, userId, "Foundation", "Isaac Asimov", synopsis: existingSynopsis);
        await db.SaveChangesAsync();

        var ollama = new FakeOllamaService("Generated Foundation context.");
        var openLibrary = new FakeOpenLibraryService("Should not be used.");
        var service = new BookContextService(db, ollama, openLibrary);

        await service.GenerateAndSaveAsync(book.Id, userId, CancellationToken.None);

        Assert.Equal(0, openLibrary.CallCount);
        Assert.Contains(existingSynopsis, ollama.LastPrompt);

        var savedBook = await db.Books.SingleAsync();
        Assert.Equal(existingSynopsis, savedBook.Synopsis);
        Assert.Equal("Generated Foundation context.", savedBook.Context);
    }

    [Fact]
    public async Task GenerateAndSaveAsync_WhenOpenLibraryReturnsNull_FallsBackToLlmGeneration()
    {
        await using var db = CreateDbContext();
        var userId = "user-1";
        var book = SeedBook(db, userId, "Unknown Book", "Unknown Author");
        await db.SaveChangesAsync();

        var ollama = new FakeOllamaService("Generated fallback context.");
        var openLibrary = new FakeOpenLibraryService(null);
        var service = new BookContextService(db, ollama, openLibrary);

        var result = await service.GenerateAndSaveAsync(book.Id, userId, CancellationToken.None);

        Assert.Equal("Generated fallback context.", result);
        Assert.Equal(1, ollama.CallCount);
        Assert.Equal(1, openLibrary.CallCount);

        var savedBook = await db.Books.SingleAsync();
        Assert.Null(savedBook.Synopsis);
        Assert.Equal("Generated fallback context.", savedBook.Context);
    }

    private static Book SeedBook(
        AppDbContext db,
        string userId,
        string title,
        string author,
        string? synopsis = null)
    {
        var book = new Book
        {
            UserId = userId,
            Title = title,
            Author = author,
            NormalizedTitle = new string(title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()),
            NormalizedAuthor = new string(author.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()),
            Synopsis = synopsis
        };

        db.Books.Add(book);
        return book;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private sealed class FakeOllamaService(string response) : IOllamaService
    {
        public string LastPrompt { get; private set; } = string.Empty;
        public int CallCount { get; private set; }

        public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        {
            CallCount++;
            LastPrompt = prompt;
            return Task.FromResult(response);
        }
    }

    private sealed class FakeOpenLibraryService(string? synopsis) : IOpenLibraryService
    {
        public int CallCount { get; private set; }

        public Task<string?> GetSynopsisAsync(string title, string author, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(synopsis);
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
