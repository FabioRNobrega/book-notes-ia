using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using WebApp.Models;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookNote> BookNotes => Set<BookNote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
    }
}
