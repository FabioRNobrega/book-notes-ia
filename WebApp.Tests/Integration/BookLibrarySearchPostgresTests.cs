using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Integration;

public class BookLibrarySearchPostgresTests
{
    [Fact]
    public async Task LibrarianSearch_WithSeededEmbedding_ReturnsClosestUserOwnedBook()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-librarian-1";
        var dune = await SeedBookAsync(db, userId, "Dune", "Frank Herbert");
        var foundation = await SeedBookAsync(db, userId, "Foundation", "Isaac Asimov");
        db.BookEmbeddings.Add(CreateEmbedding(dune, VectorWithFirstValue(1)));
        db.BookEmbeddings.Add(CreateEmbedding(foundation, VectorWithFirstValue(-1)));
        await db.SaveChangesAsync();

        var service = new LibrarianBookSearchService(db, new FakeEmbeddingService(VectorWithFirstValue(1)), NullLogger<LibrarianBookSearchService>.Instance);

        var results = await service.FindPossibleBooksAsync("desert planet", userId);

        Assert.Single(results);
        Assert.Equal("Dune", results[0].Title);
    }

    [Fact]
    public async Task LibrarianSearch_DoesNotReturnOtherUsersBooks()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userA = "user-librarian-2a";
        var userB = "user-librarian-2b";
        var bookA = await SeedBookAsync(db, userA, "Dune", "Frank Herbert");
        db.BookEmbeddings.Add(CreateEmbedding(bookA, VectorWithFirstValue(1)));
        await db.SaveChangesAsync();

        var service = new LibrarianBookSearchService(db, new FakeEmbeddingService(VectorWithFirstValue(1)), NullLogger<LibrarianBookSearchService>.Instance);

        var results = await service.FindPossibleBooksAsync("desert planet", userB);

        Assert.Empty(results);
    }

    [Fact]
    public async Task LibrarianSearch_WhenEmbeddingFails_ReturnsEmpty()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-librarian-3";
        var dune = await SeedBookAsync(db, userId, "Dune", "Frank Herbert");
        db.BookEmbeddings.Add(CreateEmbedding(dune, VectorWithFirstValue(1)));
        await db.SaveChangesAsync();

        var service = new LibrarianBookSearchService(db, new FailingEmbeddingService(), NullLogger<LibrarianBookSearchService>.Instance);

        var results = await service.FindPossibleBooksAsync("desert planet", userId);

        Assert.Empty(results);
    }

    [Fact]
    public async Task LibrarianSearch_WhenNoEmbeddingIsCloseEnough_ReturnsEmpty()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-librarian-4";
        var dune = await SeedBookAsync(db, userId, "Dune", "Frank Herbert");
        db.BookEmbeddings.Add(CreateEmbedding(dune, VectorWithFirstValue(-1)));
        await db.SaveChangesAsync();

        var service = new LibrarianBookSearchService(db, new FakeEmbeddingService(VectorWithFirstValue(1)), NullLogger<LibrarianBookSearchService>.Instance);

        var results = await service.FindPossibleBooksAsync("dessert shop", userId);

        Assert.Empty(results);
    }

    [Fact]
    public async Task LibrarianSearch_GibberishQuery_ReturnsEmptyBelowThreshold()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-librarian-5";
        var dune = await SeedBookAsync(db, userId, "Dune", "Frank Herbert");
        // Seed the book with a unit vector pointing along dimension 0
        db.BookEmbeddings.Add(CreateEmbedding(dune, VectorWithFirstValue(1)));
        await db.SaveChangesAsync();

        // Gibberish produces a near-orthogonal vector: cosine distance ≈ 1.0, well above 0.25
        var gibberishVector = OrthogonalVector();
        var service = new LibrarianBookSearchService(db, new FakeEmbeddingService(gibberishVector), NullLogger<LibrarianBookSearchService>.Instance);

        var results = await service.FindPossibleBooksAsync("asdasdfghjklqwerty", userId);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FuzzySearch_TypoInTitle_ReturnsClosestBook()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-fuzzy-1";
        await SeedBookAsync(db, userId, "The World Jones Made", "Philip K. Dick");

        var service = new BookLibrarySearchService(db);

        var result = await service.SearchSqlAsync("The Wordl Jones Made", userId);

        Assert.False(result.NoExactSqlMatch);
        Assert.Single(result.Books);
        Assert.Equal("The World Jones Made", result.Books[0].Title);
    }

    [Fact]
    public async Task FuzzySearch_TypoInAuthor_ReturnsBook()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-fuzzy-2";
        await SeedBookAsync(db, userId, "Foundation", "Isaac Asimov");

        var service = new BookLibrarySearchService(db);

        var result = await service.SearchSqlAsync("Asimof", userId);

        Assert.False(result.NoExactSqlMatch);
        Assert.Single(result.Books);
        Assert.Equal("Foundation", result.Books[0].Title);
    }

    [Fact]
    public async Task FuzzySearch_DoesNotReturnOtherUsersBooks()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userA = "user-fuzzy-3a";
        var userB = "user-fuzzy-3b";
        await SeedBookAsync(db, userA, "The World Jones Made", "Philip K. Dick");

        var service = new BookLibrarySearchService(db);

        var result = await service.SearchSqlAsync("The Wordl Jones Made", userB);

        Assert.True(result.NoExactSqlMatch);
        Assert.Empty(result.Books);
    }

    [Fact]
    public async Task FuzzySearch_CompletelyUnrelatedQuery_FallsBackToLibrarian()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-fuzzy-4";
        await SeedBookAsync(db, userId, "Dune", "Frank Herbert");

        var service = new BookLibrarySearchService(db);

        var result = await service.SearchSqlAsync("zzzzqqqq", userId);

        Assert.True(result.NoExactSqlMatch);
        Assert.Empty(result.Books);
    }

    private static async Task<Book> SeedBookAsync(AppDbContext db, string userId, string title, string author)
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

        var book = new Book
        {
            UserId = userId,
            Title = title,
            Author = author,
            NormalizedTitle = new string(title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()),
            NormalizedAuthor = new string(author.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray())
        };
        db.Books.Add(book);
        await db.SaveChangesAsync();
        return book;
    }

    private static BookEmbedding CreateEmbedding(Book book, float[] vector) =>
        new()
        {
            UserId = book.UserId,
            BookId = book.Id,
            Title = book.Title,
            Author = book.Author,
            Embedding = new Vector(vector)
        };

    private static float[] VectorWithFirstValue(float value)
    {
        var vector = new float[1024];
        vector[0] = value;
        return vector;
    }

    // Returns a unit vector orthogonal to VectorWithFirstValue(1): cosine distance = 1.0
    private static float[] OrthogonalVector()
    {
        var vector = new float[1024];
        vector[1] = 1f;
        return vector;
    }

    private sealed class FakeEmbeddingService(float[] vector) : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(vector);
    }

    private sealed class FailingEmbeddingService : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            throw new InvalidOperationException("Embedding service unavailable.");
    }
}
