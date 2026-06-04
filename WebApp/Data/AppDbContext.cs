using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using WebApp.Models;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookNote> BookNotes => Set<BookNote>();
    public DbSet<BookEmbedding> BookEmbeddings => Set<BookEmbedding>();
    public DbSet<BookNoteEmbedding> BookNoteEmbeddings => Set<BookNoteEmbedding>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("vector");

        builder.Entity<UserProfile>(e =>
        {
            e.ToTable("user_profile");

            e.HasKey(x => x.Id);

            e.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(x => x.Nickname)
             .HasMaxLength(50)
             .IsRequired();

            e.Property(x => x.PreferredLanguage)
             .HasMaxLength(10)
             .IsRequired();

            e.Property(x => x.TonePreference)
             .HasMaxLength(30);

            e.Property(x => x.LearningGoals)
             .HasMaxLength(300);

            e.Property(x => x.FavoriteAuthors)
             .HasMaxLength(200);

            e.Property(x => x.AboutMe)
             .HasMaxLength(400);

            e.Property(x => x.AgentProfileCompact)
             .IsRequired();
        });

        builder.Entity<Book>(e =>
        {
            e.ToTable("book");

            e.HasKey(x => x.Id);

            e.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(x => x.Title)
             .HasMaxLength(300)
             .IsRequired();

            e.Property(x => x.Author)
             .HasMaxLength(200)
             .IsRequired();

            e.Property(x => x.NormalizedTitle)
             .HasMaxLength(300)
             .IsRequired();

            e.Property(x => x.NormalizedAuthor)
             .HasMaxLength(200)
             .IsRequired();

            e.Property(x => x.CoverUrl)
             .HasMaxLength(1000);

            e.HasIndex(x => new { x.UserId, x.NormalizedTitle, x.NormalizedAuthor })
             .IsUnique();
        });

        builder.Entity<BookNote>(e =>
        {
            e.ToTable("book_note");

            e.HasKey(x => x.Id);

            e.HasOne(x => x.Book)
             .WithMany(x => x.Notes)
             .HasForeignKey(x => x.BookId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(x => x.EntryType)
             .HasMaxLength(20)
             .IsRequired();

            e.Property(x => x.LocationText)
             .HasMaxLength(120)
             .IsRequired();

            e.Property(x => x.Content)
             .IsRequired();

            e.Property(x => x.DedupeKey)
             .HasMaxLength(64)
             .IsRequired();

            e.HasIndex(x => new { x.UserId, x.DedupeKey })
             .IsUnique();

            e.HasIndex(x => new { x.BookId, x.ClippedAtUtc });
        });

        builder.Entity<BookEmbedding>(e =>
        {
            e.ToTable("book_embedding");

            e.HasKey(x => x.Id);

            e.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Book)
             .WithMany()
             .HasForeignKey(x => x.BookId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(x => x.Title)
             .HasMaxLength(300)
             .IsRequired();

            e.Property(x => x.Author)
             .HasMaxLength(200)
             .IsRequired();

            e.Property(x => x.Embedding)
             .HasColumnType("vector(1024)")
             .IsRequired();

            e.HasIndex(x => x.UserId);

            e.HasIndex(x => x.BookId)
             .IsUnique();

            e.HasIndex(x => x.Embedding)
             .HasMethod("hnsw")
             .HasOperators("vector_cosine_ops");
        });

        builder.Entity<BookNoteEmbedding>(e =>
        {
            e.ToTable("book_note_embedding");

            e.HasKey(x => x.Id);

            e.HasOne(x => x.Book)
             .WithMany()
             .HasForeignKey(x => x.BookId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.BookNote)
             .WithMany()
             .HasForeignKey(x => x.BookNoteId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(x => x.Embedding)
             .HasColumnType("vector(1024)")
             .IsRequired();

            e.HasIndex(x => x.UserId);

            e.HasIndex(x => new { x.UserId, x.BookId });

            e.HasIndex(x => x.BookNoteId)
             .IsUnique();

            e.HasIndex(x => x.Embedding)
             .HasMethod("hnsw")
             .HasOperators("vector_cosine_ops");
        });
    }
}
