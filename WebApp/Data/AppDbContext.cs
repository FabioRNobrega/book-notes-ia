using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using WebApp.Models;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // VERY IMPORTANT (keeps Identity mapping)

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
    }
}
