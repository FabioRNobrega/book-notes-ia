using Microsoft.EntityFrameworkCore;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class BookContextServiceTests
{
    [Fact]
    public async Task GenerateToolResponseAsync_AppendsContextAndPersistsGeneratedSummary()
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
        var service = new BookContextService(db, ollama);

        var result = await service.GenerateToolResponseAsync(book.Id, userId, "Existing context block", CancellationToken.None);

        Assert.Equal(book.Id, result.BookId);
        Assert.Equal("Generated summary from Ollama.", result.GeneratedContext);
        Assert.Contains("Existing context block", result.AppendedContext);
        Assert.Contains("[GenerateBookContext]", result.AppendedContext);
        Assert.Contains("Summary: Generated summary from Ollama.", result.AppendedContext);

        var savedBook = await db.Books.SingleAsync();
        Assert.Equal("Generated summary from Ollama.", savedBook.Context);
        Assert.Contains("Respond in Portuguese.", ollama.LastPrompt);
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

        public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        {
            LastPrompt = prompt;
            return Task.FromResult(response);
        }
    }

    private sealed class TestAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // The in-memory provider used by this unit test can't bind JsonDocument properties.
            builder.Entity<UserProfile>().Ignore(x => x.ReadingLanguages);
            builder.Entity<UserProfile>().Ignore(x => x.LearningStyle);
            builder.Entity<UserProfile>().Ignore(x => x.LovedGenres);
            builder.Entity<UserProfile>().Ignore(x => x.DislikedGenres);
        }
    }
}
