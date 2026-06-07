using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class BookLibrarySearchServiceTests
{
    [Fact]
    public async Task BlankQuery_ReturnsAllUserBooksOrderedByUpdatedAtDescending()
    {
        await using var db = CreateDbContext();
        var userId = "user-sql-1";
        var older = AddBook(db, userId, "Dune", "Frank Herbert", updatedAt: DateTime.UtcNow.AddDays(-2));
        var newer = AddBook(db, userId, "Foundation", "Isaac Asimov", updatedAt: DateTime.UtcNow.AddDays(-1));
        await db.SaveChangesAsync();

        var service = new BookLibrarySearchService(db);
        var result = await service.SearchSqlAsync(null, userId);

        Assert.False(result.NoExactSqlMatch);
        Assert.Equal(2, result.Books.Count);
        Assert.Equal(newer.Id, result.Books[0].Id);
        Assert.Equal(older.Id, result.Books[1].Id);
    }

    [Fact]
    public async Task EmptyStringQuery_ReturnsAllUserBooks()
    {
        await using var db = CreateDbContext();
        var userId = "user-sql-2";
        AddBook(db, userId, "Dune", "Frank Herbert");
        await db.SaveChangesAsync();

        var service = new BookLibrarySearchService(db);
        var result = await service.SearchSqlAsync("   ", userId);

        Assert.False(result.NoExactSqlMatch);
        Assert.Single(result.Books);
    }

    [Fact]
    public async Task TitleSearch_MatchesCaseInsensitively()
    {
        await using var db = CreateDbContext();
        var userId = "user-sql-3";
        AddBook(db, userId, "Dune", "Frank Herbert");
        AddBook(db, userId, "Foundation", "Isaac Asimov");
        await db.SaveChangesAsync();

        var service = new BookLibrarySearchService(db);
        var result = await service.SearchSqlAsync("dune", userId);

        Assert.False(result.NoExactSqlMatch);
        Assert.Single(result.Books);
        Assert.Equal("Dune", result.Books[0].Title);
    }

    [Fact]
    public async Task AuthorSearch_MatchesCaseInsensitively()
    {
        await using var db = CreateDbContext();
        var userId = "user-sql-4";
        AddBook(db, userId, "Dune", "Frank Herbert");
        AddBook(db, userId, "Foundation", "Isaac Asimov");
        await db.SaveChangesAsync();

        var service = new BookLibrarySearchService(db);
        var result = await service.SearchSqlAsync("ASIMOV", userId);

        Assert.False(result.NoExactSqlMatch);
        Assert.Single(result.Books);
        Assert.Equal("Foundation", result.Books[0].Title);
    }

    [Fact]
    public async Task SqlSearch_ExcludesBooksFromOtherUsers()
    {
        await using var db = CreateDbContext();
        var userA = "user-sql-5a";
        var userB = "user-sql-5b";
        AddBook(db, userA, "Dune", "Frank Herbert");
        AddBook(db, userB, "Foundation", "Isaac Asimov");
        await db.SaveChangesAsync();

        var service = new BookLibrarySearchService(db);
        var result = await service.SearchSqlAsync("dune", userB);

        Assert.True(result.NoExactSqlMatch);
        Assert.Empty(result.Books);
    }

    [Fact]
    public async Task NonMatchingQuery_ReturnsNoExactSqlMatch()
    {
        await using var db = CreateDbContext();
        var userId = "user-sql-6";
        AddBook(db, userId, "Dune", "Frank Herbert");
        await db.SaveChangesAsync();

        var service = new BookLibrarySearchService(db);
        var result = await service.SearchSqlAsync("Tolkien", userId);

        Assert.True(result.NoExactSqlMatch);
        Assert.Empty(result.Books);
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
