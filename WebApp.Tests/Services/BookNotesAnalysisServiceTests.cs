using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        var service = new BookNotesAnalysisService(db, new ConfigurationBuilder().Build());

        var result = await service.GetNotesAsync(book, userId, CancellationToken.None);

        Assert.Contains("Notes for \"The Left Hand of Darkness\"", result);
        Assert.Contains("2 highlights", result);
        Assert.Contains("<note>Estraven moves between loyalty and exile.</note>", result);
        Assert.Contains("<note>The cold reshapes politics and trust.</note>", result);
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

        var service = new BookNotesAnalysisService(db, new ConfigurationBuilder().Build());

        var result = await service.GetNotesAsync(book, userId, CancellationToken.None);

        Assert.Contains("<note>Fear is the mind-killer.</note>", result);
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
            SourceBookTitle = title,
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
