using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class BookTitleServiceTests
{
    [Fact]
    public async Task UpdateTitleAsync_WithValidTitle_TrimsAndPersistsTitle()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "user-1", "Old Title", "Author");
        await db.SaveChangesAsync();

        var service = new BookTitleService(db);

        var result = await service.UpdateTitleAsync(book.Id, "user-1", "  New Title  ");

        Assert.Equal(BookTitleUpdateStatus.Success, result.Status);
        Assert.Equal("New Title", book.Title);
    }

    [Fact]
    public async Task UpdateTitleAsync_WithValidTitle_UpdatesNormalizedTitle()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "user-1", "Old Title", "Author");
        await db.SaveChangesAsync();

        var service = new BookTitleService(db);

        await service.UpdateTitleAsync(book.Id, "user-1", "The Left Hand of Darkness!");

        Assert.Equal("thelefthandofdarkness", book.NormalizedTitle);
    }

    [Fact]
    public async Task UpdateTitleAsync_WithValidTitle_UpdatesUpdatedAt()
    {
        await using var db = CreateDbContext();
        var originalUpdatedAt = DateTime.UtcNow.AddDays(-2);
        var book = AddBook(db, "user-1", "Old Title", "Author", originalUpdatedAt);
        await db.SaveChangesAsync();

        var service = new BookTitleService(db);

        await service.UpdateTitleAsync(book.Id, "user-1", "New Title");

        Assert.True(book.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateTitleAsync_WithWhitespaceTitle_ReturnsValidationErrorAndDoesNotMutateBook()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "user-1", "Old Title", "Author");
        await db.SaveChangesAsync();

        var service = new BookTitleService(db);

        var result = await service.UpdateTitleAsync(book.Id, "user-1", "   ");

        Assert.Equal(BookTitleUpdateStatus.ValidationError, result.Status);
        Assert.Equal("Old Title", book.Title);
        Assert.Equal("oldtitle", book.NormalizedTitle);
    }

    [Fact]
    public async Task UpdateTitleAsync_WithOtherUserBook_ReturnsNotFoundAndDoesNotMutateBook()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "other-user", "Old Title", "Author");
        await db.SaveChangesAsync();

        var service = new BookTitleService(db);

        var result = await service.UpdateTitleAsync(book.Id, "user-1", "New Title");

        Assert.Equal(BookTitleUpdateStatus.NotFound, result.Status);
        Assert.Equal("Old Title", book.Title);
        Assert.Equal("oldtitle", book.NormalizedTitle);
    }

    [Fact]
    public async Task UpdateTitleAsync_WithMissingBook_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var service = new BookTitleService(db);

        var result = await service.UpdateTitleAsync(Guid.NewGuid(), "user-1", "New Title");

        Assert.Equal(BookTitleUpdateStatus.NotFound, result.Status);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;
        return new TestDbContext(options);
    }

    private static Book AddBook(AppDbContext db, string userId, string title, string author, DateTime? updatedAt = null)
    {
        var book = new Book
        {
            UserId = userId,
            Title = title,
            Author = author,
            NormalizedTitle = new string(title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()),
            NormalizedAuthor = new string(author.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()),
            UpdatedAt = updatedAt ?? DateTime.UtcNow
        };
        db.Books.Add(book);
        return book;
    }

    private sealed class TestDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
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
