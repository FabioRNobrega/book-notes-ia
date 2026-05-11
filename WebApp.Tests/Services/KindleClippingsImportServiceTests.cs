using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using WebApp.Models;
using WebApp.Services;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Services;

public class KindleClippingsImportServiceTests
{
    [Fact]
    public async Task ImportAsync_WhenNewBooksImported_InsertsBookEmbeddingForEachNewBook()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-import-1";
        await SeedUserAsync(db, userId);
        var embeddingService = new FakeEmbeddingService();
        var service = new KindleClippingsImportService(db, embeddingService);

        var summary = await service.ImportAsync(userId, ToStream(TwoBookClippings()), CancellationToken.None);

        Assert.Equal(2, summary.BooksTouched);
        Assert.Equal(2, await db.BookEmbeddings.CountAsync(e => e.UserId == userId));
        Assert.Equal(2, embeddingService.EmbedCallCount);
    }

    [Fact]
    public async Task ImportAsync_WhenBookAlreadyExists_DoesNotInsertDuplicateEmbedding()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.CreateDbContext();
        var userId = "user-import-2";
        await SeedUserAsync(db, userId);
        var book = new Book
        {
            UserId = userId,
            Title = "Dune",
            Author = "Frank Herbert",
            NormalizedTitle = "dune",
            NormalizedAuthor = "frank herbert"
        };
        db.Books.Add(book);
        await db.SaveChangesAsync();
        db.BookEmbeddings.Add(new BookEmbedding
        {
            UserId = userId,
            BookId = book.Id,
            Title = book.Title,
            Author = book.Author,
            Embedding = new Vector(FakeEmbeddingService.Vector())
        });
        await db.SaveChangesAsync();

        var embeddingService = new FakeEmbeddingService();
        var service = new KindleClippingsImportService(db, embeddingService);

        await service.ImportAsync(userId, ToStream(DuneClipping()), CancellationToken.None);

        Assert.Equal(1, await db.BookEmbeddings.CountAsync(e => e.UserId == userId));
        Assert.Equal(0, embeddingService.EmbedCallCount);
    }

    private static async Task SeedUserAsync(AppDbContext db, string userId)
    {
        db.Users.Add(new IdentityUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            NormalizedUserName = $"{userId}@EXAMPLE.TEST",
            Email = $"{userId}@example.test",
            NormalizedEmail = $"{userId}@EXAMPLE.TEST"
        });
        await db.SaveChangesAsync();
    }

    private static MemoryStream ToStream(string text) =>
        new(Encoding.UTF8.GetBytes(text));

    private static string TwoBookClippings() =>
        DuneClipping() +
        """
        ==========
        Foundation (Isaac Asimov)
        - Seu destaque na página 2 | posição 20-21 | Adicionado: 01/01/2023 12:05:00

        Psychohistory.
        """;

    private static string DuneClipping() =>
        """
        Dune (Frank Herbert)
        - Seu destaque na página 1 | posição 10-11 | Adicionado: 01/01/2023 12:00:00

        Fear is the mind-killer.

        """;

    private sealed class FakeEmbeddingService : IEmbeddingService
    {
        public int EmbedCallCount { get; private set; }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            EmbedCallCount++;
            return Task.FromResult(Vector());
        }

        public static float[] Vector()
        {
            var vector = new float[1024];
            vector[0] = 1;
            return vector;
        }
    }
}
