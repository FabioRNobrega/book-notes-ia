using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Controllers;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Controllers;

public class NotesControllerTests
{
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

    private static NotesController CreateController(
        bool authenticated = true,
        IBookLibrarySearchService? librarySearch = null,
        ILibrarianBookSearchService? librarianSearch = null)
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;
        var db = new TestDbContext(dbOptions);

        var controller = new NotesController(
            db,
            new FakeImportService(),
            new FakeBookContextService(),
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
        public Task<string> GenerateAndSaveAsync(Guid bookId, string userId, CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<string> SaveManualAsync(Guid bookId, string userId, string context) => Task.FromResult(context);
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
