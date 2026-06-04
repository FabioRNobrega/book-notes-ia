using Microsoft.EntityFrameworkCore;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class BookNotesAnalysisServiceTests
{
    [Fact]
    public async Task GetNotesWithAnalysisAsync_BuildsPromptWithBookContextAndNotes()
    {
        await using var db = CreateDbContext();
        var userId = "user-1";
        var book = SeedBook(db, userId, "The Left Hand of Darkness", "Ursula K. Le Guin", "Gethen context.");
        SeedProfile(db, userId, "English");
        SeedNote(db, book, "Estraven moves between loyalty and exile.", DateTime.UtcNow.AddMinutes(-2));
        SeedNote(db, book, "The cold reshapes politics and trust.", DateTime.UtcNow.AddMinutes(-1));
        await db.SaveChangesAsync();

        var ollama = new FakeOllamaService("Generated analysis.");
        var service = new BookNotesAnalysisService(db, ollama);

        var result = await service.GetNotesWithAnalysisAsync(book, userId, CancellationToken.None);

        Assert.Contains("Thematic analysis for", result);
        Assert.Contains("Generated analysis.", result);
        Assert.DoesNotContain("<note>Estraven moves between loyalty and exile.</note>", result);
        Assert.Contains("Book Context:", ollama.LastPrompt);
        Assert.Contains("Gethen context.", ollama.LastPrompt);
        Assert.Contains("<note>Estraven moves between loyalty and exile.</note>", ollama.LastPrompt);
        Assert.Contains("<note>The cold reshapes politics and trust.</note>", ollama.LastPrompt);
        Assert.Equal(1, ollama.CallCount);
    }

    [Fact]
    public async Task GetNotesWithAnalysisAsync_WhenContextIsNull_UsesNotAvailablePlaceholder()
    {
        await using var db = CreateDbContext();
        var userId = "user-1";
        var book = SeedBook(db, userId, "Dune", "Frank Herbert");
        SeedProfile(db, userId, "English");
        SeedNote(db, book, "Fear is the mind-killer.", DateTime.UtcNow);
        await db.SaveChangesAsync();

        var ollama = new FakeOllamaService("Generated analysis.");
        var service = new BookNotesAnalysisService(db, ollama);

        await service.GetNotesWithAnalysisAsync(book, userId, CancellationToken.None);

        Assert.Contains("Not available.", ollama.LastPrompt);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static Book SeedBook(
        AppDbContext db,
        string userId,
        string title,
        string author,
        string? context = null)
    {
        var book = new Book
        {
            UserId = userId,
            Title = title,
            Author = author,
            NormalizedTitle = NormalizeKey(title),
            NormalizedAuthor = NormalizeKey(author),
            Context = context
        };

        db.Books.Add(book);
        return book;
    }

    private static void SeedProfile(AppDbContext db, string userId, string preferredLanguage)
    {
        db.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            Nickname = "Reader",
            PreferredLanguage = preferredLanguage,
            AgentProfileCompact = "{}"
        });
    }

    private static void SeedNote(AppDbContext db, Book book, string content, DateTime clippedAtUtc)
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

    private static string NormalizeKey(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private sealed class FakeOllamaService(string response) : IOllamaService
    {
        public int CallCount { get; private set; }
        public string LastPrompt { get; private set; } = string.Empty;

        public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        {
            CallCount++;
            LastPrompt = prompt;
            return Task.FromResult(response);
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
        }
    }
}
