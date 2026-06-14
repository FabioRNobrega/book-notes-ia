using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Controllers;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Controllers;

public class UserProfileControllerTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ProfileTestDbContext(opts);
    }

    private static UserProfileController CreateController(AppDbContext db, string userId)
    {
        var controller = new UserProfileController(db, NullLogger<UserProfileController>.Instance, new FakeCacheHandler());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuth"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task Upsert_WhenCreating_PersistsFemaleVoicePreference()
    {
        var db = CreateDb();
        var controller = CreateController(db, "user-1");

        await controller.Upsert(
            new UserProfile { Nickname = "Test", PreferredLanguage = "en", VoicePreference = "female" },
            null, null, null, null);

        var saved = await db.UserProfiles.FirstAsync();
        Assert.Equal("female", saved.VoicePreference);
    }

    [Fact]
    public async Task Upsert_WhenCreating_PersistsMaleVoicePreference()
    {
        var db = CreateDb();
        var controller = CreateController(db, "user-1");

        await controller.Upsert(
            new UserProfile { Nickname = "Test", PreferredLanguage = "en", VoicePreference = "male" },
            null, null, null, null);

        var saved = await db.UserProfiles.FirstAsync();
        Assert.Equal("male", saved.VoicePreference);
    }

    [Fact]
    public async Task Upsert_WhenVoicePreferenceOmitted_DefaultsToFemale()
    {
        var db = CreateDb();
        var controller = CreateController(db, "user-1");

        // VoicePreference defaults to "female" on the model
        await controller.Upsert(
            new UserProfile { Nickname = "Test", PreferredLanguage = "en" },
            null, null, null, null);

        var saved = await db.UserProfiles.FirstAsync();
        Assert.Equal("female", saved.VoicePreference);
    }

    [Fact]
    public async Task Upsert_WhenUpdating_OverwritesVoicePreference()
    {
        var db = CreateDb();
        var existing = new UserProfile
        {
            UserId = "user-1",
            Nickname = "Old",
            PreferredLanguage = "en",
            VoicePreference = "female",
            AgentProfileCompact = "{}"
        };
        db.UserProfiles.Add(existing);
        await db.SaveChangesAsync();

        var controller = CreateController(db, "user-1");
        await controller.Upsert(
            new UserProfile { Nickname = "Updated", PreferredLanguage = "pt", VoicePreference = "male" },
            null, null, null, null);

        var saved = await db.UserProfiles.FirstAsync();
        Assert.Equal("male", saved.VoicePreference);
    }

    [Fact]
    public async Task Upsert_InvalidVoicePreference_DefaultsToFemale()
    {
        var db = CreateDb();
        var controller = CreateController(db, "user-1");

        await controller.Upsert(
            new UserProfile { Nickname = "Test", PreferredLanguage = "en", VoicePreference = "unknown" },
            null, null, null, null);

        var saved = await db.UserProfiles.FirstAsync();
        Assert.Equal("female", saved.VoicePreference);
    }

    private sealed class ProfileTestDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
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

    private sealed class FakeCacheHandler : ICacheHandler
    {
        private readonly Dictionary<string, string> _store = [];
        public Task<string?> GetAsync(string key, CancellationToken ct = default) => Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);
        public Task RemoveAsync(string key, CancellationToken ct = default) { _store.Remove(key); return Task.CompletedTask; }
        public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default) { _store[key] = value; return Task.CompletedTask; }
        public Task SetObjectAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) { _store[key] = System.Text.Json.JsonSerializer.Serialize(value); return Task.CompletedTask; }
    }
}
