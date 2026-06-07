using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
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

        var service = CreateService(db);

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

        var service = CreateService(db);

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

        var service = CreateService(db);

        await service.UpdateTitleAsync(book.Id, "user-1", "New Title");

        Assert.True(book.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateTitleAsync_WithWhitespaceTitle_ReturnsValidationErrorAndDoesNotMutateBook()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "user-1", "Old Title", "Author");
        await db.SaveChangesAsync();

        var service = CreateService(db);

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

        var service = CreateService(db);

        var result = await service.UpdateTitleAsync(book.Id, "user-1", "New Title");

        Assert.Equal(BookTitleUpdateStatus.NotFound, result.Status);
        Assert.Equal("Old Title", book.Title);
        Assert.Equal("oldtitle", book.NormalizedTitle);
    }

    [Fact]
    public async Task UpdateTitleAsync_WithMissingBook_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.UpdateTitleAsync(Guid.NewGuid(), "user-1", "New Title");

        Assert.Equal(BookTitleUpdateStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateTitleAsync_WithValidTitle_UpdatesExistingBookEmbeddingTitleAndVector()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "user-1", "Old Title", "Author");
        var originalVector = new[] { 0.1f, 0.2f, 0.3f };
        db.BookEmbeddings.Add(CreateBookEmbedding(book, originalVector));
        await db.SaveChangesAsync();
        var embeddingService = new FakeEmbeddingService([0.7f, 0.8f, 0.9f]);
        var service = CreateService(db, embeddingService);

        await service.UpdateTitleAsync(book.Id, "user-1", "New Title");

        var embedding = await db.BookEmbeddings.SingleAsync(x => x.BookId == book.Id && x.UserId == "user-1");
        Assert.Equal("New Title", embedding.Title);
        Assert.Equal([0.7f, 0.8f, 0.9f], embedding.Embedding.ToArray());
        Assert.Equal("Author", embedding.Author);
        Assert.Equal("New Title by Author", embeddingService.LastText);
    }

    [Fact]
    public async Task UpdateTitleAsync_WithValidTitle_CreatesBookEmbeddingWhenNoneExists()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "user-1", "Old Title", "Author");
        await db.SaveChangesAsync();
        var embeddingService = new FakeEmbeddingService([0.4f, 0.5f, 0.6f]);
        var service = CreateService(db, embeddingService);

        await service.UpdateTitleAsync(book.Id, "user-1", "New Title");

        var embedding = await db.BookEmbeddings.SingleAsync(x => x.BookId == book.Id && x.UserId == "user-1");
        Assert.Equal(book.Id, embedding.BookId);
        Assert.Equal("user-1", embedding.UserId);
        Assert.Equal("New Title", embedding.Title);
        Assert.Equal("Author", embedding.Author);
        Assert.Equal([0.4f, 0.5f, 0.6f], embedding.Embedding.ToArray());
    }

    [Fact]
    public async Task UpdateTitleAsync_WhenEmbeddingServiceThrows_StillPersistsTitleAndReturnsSuccess()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "user-1", "Old Title", "Author");
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeEmbeddingService(throws: true));

        var result = await service.UpdateTitleAsync(book.Id, "user-1", "New Title");

        Assert.Equal(BookTitleUpdateStatus.Success, result.Status);
        var updatedBook = await db.Books.SingleAsync(x => x.Id == book.Id);
        Assert.Equal("New Title", updatedBook.Title);
        Assert.Empty(await db.BookEmbeddings.Where(x => x.BookId == book.Id).ToListAsync());
    }

    [Fact]
    public async Task UpdateTitleAsync_WhenEmbeddingServiceThrows_LogsWarning()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "user-1", "Old Title", "Author");
        await db.SaveChangesAsync();
        var logger = new TestLogger<BookTitleService>();
        var service = CreateService(db, new FakeEmbeddingService(throws: true), logger);

        await service.UpdateTitleAsync(book.Id, "user-1", "New Title");

        Assert.Contains(logger.Entries, entry => entry.LogLevel == LogLevel.Warning);
    }

    private static BookTitleService CreateService(
        AppDbContext db,
        IEmbeddingService? embeddingService = null,
        ILogger<BookTitleService>? logger = null) =>
        new(db, embeddingService ?? new FakeEmbeddingService(), logger ?? NullLogger<BookTitleService>.Instance);

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
            SourceBookTitle = title,
            Author = author,
            NormalizedTitle = new string(title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()),
            NormalizedAuthor = new string(author.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()),
            UpdatedAt = updatedAt ?? DateTime.UtcNow
        };
        db.Books.Add(book);
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

    private sealed class FakeEmbeddingService(float[]? vector = null, bool throws = false) : IEmbeddingService
    {
        public string? LastText { get; private set; }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            LastText = text;

            if (throws)
                throw new InvalidOperationException("Embedding unavailable.");

            return Task.FromResult(vector ?? [0.1f, 0.2f, 0.3f]);
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    private sealed class TestDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<UserProfile>().Ignore(x => x.ReadingLanguages);
            builder.Entity<UserProfile>().Ignore(x => x.LearningStyle);
            builder.Entity<UserProfile>().Ignore(x => x.LovedGenres);
            builder.Entity<UserProfile>().Ignore(x => x.DislikedGenres);
            builder.Entity<BookEmbedding>()
                .Property(x => x.Embedding)
                .HasConversion(x => SerializeVector(x), x => DeserializeVector(x));
            builder.Ignore<BookNoteEmbedding>();
        }

        private static string SerializeVector(Vector vector) =>
            string.Join(",", vector.ToArray());

        private static Vector DeserializeVector(string value) =>
            new(value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(float.Parse).ToArray());
    }
}
