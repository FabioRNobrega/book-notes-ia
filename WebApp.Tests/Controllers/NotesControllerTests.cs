using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using WebApp.Controllers;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Controllers;

public class NotesControllerTests
{
    [Fact]
    public async Task Library_ReturnsBooksOrderedByTitleAscending()
    {
        await using var db = CreateDbContext();
        AddBook(db, "test-user", "O Estrangeiro", "Albert Camus", updatedAt: DateTime.UtcNow);
        AddBook(db, "test-user", "14 Hábitos de Desenvolvedores Altamente Produtivos", "Zeno Rocha", updatedAt: DateTime.UtcNow.AddDays(-2));
        AddBook(db, "test-user", "A Lei", "Frédéric Bastiat", updatedAt: DateTime.UtcNow.AddDays(-1));
        await db.SaveChangesAsync();
        var controller = CreateController(db: db);

        var result = await controller.Library(CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("~/Views/Home/_SeeYourNotes.cshtml", partial.ViewName);
        var model = Assert.IsType<NotesLibraryViewModel>(partial.Model);
        Assert.Equal(
            ["14 Hábitos de Desenvolvedores Altamente Produtivos", "A Lei", "O Estrangeiro"],
            model.Books.Select(b => b.Title));
    }

    [Fact]
    public async Task LibrarySearch_Unauthenticated_ReturnsUnauthorized()
    {
        var controller = CreateController(authenticated: false);

        var result = await controller.LibrarySearch(null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task LibrarySearch_WithSqlMatches_ReturnsResultsPartial()
    {
        var books = new List<BookCardViewModel> { new() { Id = Guid.NewGuid(), Title = "Dune", Author = "Frank Herbert" } };
        var controller = CreateController(
            librarySearch: new FakeLibrarySearchService(new LibrarySearchResult(books, NoExactSqlMatch: false)));

        var result = await controller.LibrarySearch("dune", CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Contains("_BookLibraryResults", partial.ViewName!);
        var model = Assert.IsType<BookLibraryResultsViewModel>(partial.Model);
        Assert.Single(model.Books);
        Assert.False(model.IsLibrarianNotFound);
        Assert.Null(model.HeaderMessage);
    }

    [Fact]
    public async Task LibrarySearch_WithNoSqlMatches_ReturnsLibrarianSearchingPartial_NotFinalNoResults()
    {
        var controller = CreateController(
            librarySearch: new FakeLibrarySearchService(new LibrarySearchResult([], NoExactSqlMatch: true)));

        var result = await controller.LibrarySearch("Tolkien", CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Contains("_BookLibraryLibrarianSearch", partial.ViewName!);
        Assert.Equal("Tolkien", partial.Model);
    }

    [Fact]
    public async Task LibrarianSearch_Unauthenticated_ReturnsUnauthorized()
    {
        var controller = CreateController(authenticated: false);

        var result = await controller.LibrarianSearch("query", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task LibrarianSearch_WithFoundBooks_ReturnsResultsPartialWithLibrarianHeader()
    {
        var books = new List<BookCardViewModel> { new() { Id = Guid.NewGuid(), Title = "Dune", Author = "Frank Herbert" } };
        var controller = CreateController(
            librarianSearch: new FakeLibrarianSearchService(books));

        var result = await controller.LibrarianSearch("desert planet", CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Contains("_BookLibraryResults", partial.ViewName!);
        var model = Assert.IsType<BookLibraryResultsViewModel>(partial.Model);
        Assert.Single(model.Books);
        Assert.Equal("Here's what our librarian found for you!", model.HeaderMessage);
        Assert.False(model.IsLibrarianNotFound);
    }

    [Fact]
    public async Task LibrarianSearch_WithNoBooks_ReturnsLibrarianNotFoundState()
    {
        var controller = CreateController(
            librarianSearch: new FakeLibrarianSearchService([]));

        var result = await controller.LibrarianSearch("zzz-no-match", CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Contains("_BookLibraryResults", partial.ViewName!);
        var model = Assert.IsType<BookLibraryResultsViewModel>(partial.Model);
        Assert.Empty(model.Books);
        Assert.True(model.IsLibrarianNotFound);
        Assert.Null(model.HeaderMessage);
    }

    [Fact]
    public async Task EditTitle_WithAuthenticatedOwner_ReturnsBookTitlePartialInEditMode()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "test-user", "Dune", "Frank Herbert");
        await db.SaveChangesAsync();
        var controller = CreateController(db: db);

        var result = await controller.EditTitle(book.Id, CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("~/Views/Notes/_BookTitle.cshtml", partial.ViewName);
        var model = Assert.IsType<BookTitleEditViewModel>(partial.Model);
        Assert.Equal(book.Id, model.Id);
        Assert.Equal("Dune", model.Title);
        Assert.True(model.IsEditing);
    }

    [Fact]
    public async Task EditTitle_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(authenticated: false);

        var result = await controller.EditTitle(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task EditTitle_WithMissingOrOtherUserBook_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "other-user", "Dune", "Frank Herbert");
        await db.SaveChangesAsync();
        var controller = CreateController(db: db);

        var otherUserResult = await controller.EditTitle(book.Id, CancellationToken.None);
        var missingResult = await controller.EditTitle(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(otherUserResult);
        Assert.IsType<NotFoundResult>(missingResult);
    }

    [Fact]
    public async Task UpdateTitle_WithValidTitle_ReturnsBookTitlePartialInViewMode()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "test-user", "Dune", "Frank Herbert");
        await db.SaveChangesAsync();
        var controller = CreateController(db: db);

        var result = await controller.UpdateTitle(book.Id, "  Dune Messiah  ", CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("~/Views/Notes/_BookTitle.cshtml", partial.ViewName);
        var model = Assert.IsType<BookTitleEditViewModel>(partial.Model);
        Assert.Equal("Dune Messiah", model.Title);
        Assert.False(model.IsEditing);
    }

    [Fact]
    public async Task UpdateTitle_WithWhitespaceTitle_ReturnsBookTitlePartialInEditModeWithError()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "test-user", "Dune", "Frank Herbert");
        await db.SaveChangesAsync();
        var controller = CreateController(db: db);

        var result = await controller.UpdateTitle(book.Id, "   ", CancellationToken.None);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("~/Views/Notes/_BookTitle.cshtml", partial.ViewName);
        var model = Assert.IsType<BookTitleEditViewModel>(partial.Model);
        Assert.Equal("Dune", model.Title);
        Assert.True(model.IsEditing);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public async Task UpdateTitle_WithoutUser_ReturnsUnauthorized()
    {
        var controller = CreateController(authenticated: false);

        var result = await controller.UpdateTitle(Guid.NewGuid(), "New Title", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task UpdateTitle_WithMissingOrOtherUserBook_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var book = AddBook(db, "other-user", "Dune", "Frank Herbert");
        await db.SaveChangesAsync();
        var controller = CreateController(db: db);

        var otherUserResult = await controller.UpdateTitle(book.Id, "New Title", CancellationToken.None);
        var missingResult = await controller.UpdateTitle(Guid.NewGuid(), "New Title", CancellationToken.None);

        Assert.IsType<NotFoundResult>(otherUserResult);
        Assert.IsType<NotFoundResult>(missingResult);
    }

    private static NotesController CreateController(
        bool authenticated = true,
        AppDbContext? db = null,
        IBookTitleService? bookTitleService = null,
        IBookLibrarySearchService? librarySearch = null,
        ILibrarianBookSearchService? librarianSearch = null)
    {
        db ??= CreateDbContext();

        var controller = new NotesController(
            db,
            new FakeImportService(),
            new FakeBookContextService(),
            bookTitleService ?? new BookTitleService(
                db,
                new FakeEmbeddingService(),
                NullLogger<BookTitleService>.Instance),
            librarySearch ?? new FakeLibrarySearchService(new LibrarySearchResult([], NoExactSqlMatch: false)),
            librarianSearch ?? new FakeLibrarianSearchService([]),
            NullLogger<NotesController>.Instance,
            new ConfigurationBuilder().Build());

        var identity = authenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], "TestAuth")
            : new ClaimsIdentity();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
    }

    private static AppDbContext CreateDbContext()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;
        return new TestDbContext(dbOptions);
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

    private sealed class FakeLibrarySearchService(LibrarySearchResult result) : IBookLibrarySearchService
    {
        public Task<LibrarySearchResult> SearchSqlAsync(string? query, string userId, CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeLibrarianSearchService(IReadOnlyList<BookCardViewModel> books) : ILibrarianBookSearchService
    {
        public Task<IReadOnlyList<BookCardViewModel>> FindPossibleBooksAsync(string query, string userId, CancellationToken ct = default) =>
            Task.FromResult(books);
    }

    private sealed class FakeImportService : IKindleClippingsImportService
    {
        public Task<KindleImportSummary> ImportAsync(string userId, Stream stream, CancellationToken ct = default) =>
            Task.FromResult(new KindleImportSummary(0, 0, 0, 0));
    }

    private sealed class FakeBookContextService : IBookContextService
    {
        public Task ClearAsync(Guid bookId, string userId) => Task.CompletedTask;
        public Task<string?> GetContextAsync(Guid bookId, string userId) => Task.FromResult<string?>(null);
        public Task<string> GenerateAndSaveAsync(Guid bookId, string userId, string agentKey, CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<string> SaveManualAsync(Guid bookId, string userId, string context) => Task.FromResult(context);
    }

    private sealed class FakeEmbeddingService : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(new[] { 0.1f, 0.2f, 0.3f });
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
