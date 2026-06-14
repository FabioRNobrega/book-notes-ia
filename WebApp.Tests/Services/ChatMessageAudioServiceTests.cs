using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class ChatMessageAudioServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AudioTestDbContext(opts);
    }

    private static ChatMessageAudioService CreateService(
        AppDbContext db,
        ITtsClient? ttsClient = null,
        IAudioStorage? storage = null) =>
        new(db,
            ttsClient ?? new FakeTtsClient(new byte[] { 0x52, 0x49, 0x46, 0x46 }),
            storage ?? new FakeAudioStorage(),
            NullLogger<ChatMessageAudioService>.Instance);

    private static async Task<(AppDbContext Db, ChatMessage AssistantMsg)> SeedAssistantMessage(
        string userId = "user-1",
        string? languageOverride = null)
    {
        var db = CreateDb();
        var msg = new ChatMessage
        {
            UserId = userId,
            SessionId = Guid.NewGuid(),
            Role = "assistant",
            Content = "Hello from the assistant.",
            DisplayOrder = 1
        };
        db.ChatMessages.Add(msg);

        if (languageOverride is not null)
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Nickname = "Test",
                PreferredLanguage = languageOverride,
                VoicePreference = "female",
                AgentProfileCompact = "{}"
            });
        }

        await db.SaveChangesAsync();
        return (db, msg);
    }

    // ── language/voice normalisation ─────────────────────────────────────────

    [Theory]
    [InlineData("pt-BR", "pt")]
    [InlineData("pt-br", "pt")]
    [InlineData("portuguese", "pt")]
    [InlineData("en", "en")]
    [InlineData("english", "en")]
    public void NormalizeLanguage_ReturnsExpected(string input, string expected) =>
        Assert.Equal(expected, ChatMessageAudioService.NormalizeLanguage(input));

    [Theory]
    [InlineData("female", "female")]
    [InlineData("male", "male")]
    [InlineData("", "female")]
    [InlineData("other", "female")]
    public void NormalizeVoiceGender_ReturnsExpected(string input, string expected) =>
        Assert.Equal(expected, ChatMessageAudioService.NormalizeVoiceGender(input));

    [Theory]
    [InlineData("female", "F3")]
    [InlineData("male", "M3")]
    public void ResolveVoiceName_ReturnsExpected(string gender, string expected) =>
        Assert.Equal(expected, ChatMessageAudioService.ResolveVoiceName(gender));

    // ── cache hit ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAudioAsync_WhenCached_ReturnsCachedBytesWithoutCallingTts()
    {
        var (db, msg) = await SeedAssistantMessage();
        var cachedBytes = new byte[] { 9, 8, 7 };
        var storage = new FakeAudioStorage();
        await storage.WriteAsync("audio/cached.wav", cachedBytes);

        db.ChatMessageAudios.Add(new ChatMessageAudio
        {
            ChatMessageId = msg.Id,
            Language = "en",
            Voice = "F3",
            StorageKey = "audio/cached.wav",
            ContentType = "audio/wav",
            ByteLength = cachedBytes.Length
        });
        await db.SaveChangesAsync();

        var tts = new SpyTtsClient();
        var svc = CreateService(db, tts, storage);

        var result = await svc.GetOrCreateAudioAsync("user-1", msg.Id);

        Assert.NotNull(result);
        Assert.Equal(cachedBytes, result.Value.WavBytes);
        Assert.False(tts.WasCalled, "TTS should not be called on a cache hit");
    }

    // ── cache miss ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAudioAsync_WhenNoCache_CallsTtsAndPersistsMetadata()
    {
        var (db, msg) = await SeedAssistantMessage();
        var wavData = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        var tts = new FakeTtsClient(wavData);
        var storage = new FakeAudioStorage();
        var svc = CreateService(db, tts, storage);

        var result = await svc.GetOrCreateAudioAsync("user-1", msg.Id);

        Assert.NotNull(result);
        Assert.Equal(wavData, result.Value.WavBytes);
        Assert.Equal("audio/wav", result.Value.ContentType);

        var persisted = await db.ChatMessageAudios.FirstOrDefaultAsync();
        Assert.NotNull(persisted);
        Assert.Equal(msg.Id, persisted.ChatMessageId);
        Assert.Equal("en", persisted.Language);
        Assert.Equal("F3", persisted.Voice);
    }

    // ── language from profile ─────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAudioAsync_UsesProfilePreferredLanguage()
    {
        var (db, msg) = await SeedAssistantMessage(languageOverride: "pt-BR");
        var tts = new SpyTtsClient();
        var svc = CreateService(db, tts);

        await svc.GetOrCreateAudioAsync("user-1", msg.Id);

        Assert.Equal("pt", tts.LastLanguage);
    }

    // ── user isolation ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAudioAsync_WhenMessageBelongsToOtherUser_ReturnsNull()
    {
        var (db, msg) = await SeedAssistantMessage(userId: "user-1");
        var svc = CreateService(db);

        var result = await svc.GetOrCreateAudioAsync("user-2", msg.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrCreateAudioAsync_WhenMessageIsUserRole_ReturnsNull()
    {
        var db = CreateDb();
        var msg = new ChatMessage
        {
            UserId = "user-1",
            SessionId = Guid.NewGuid(),
            Role = "user",
            Content = "A user message",
            DisplayOrder = 1
        };
        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.GetOrCreateAudioAsync("user-1", msg.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrCreateAudioAsync_WhenMessageDoesNotExist_ReturnsNull()
    {
        var svc = CreateService(CreateDb());
        var result = await svc.GetOrCreateAudioAsync("user-1", Guid.NewGuid());
        Assert.Null(result);
    }

    // ── fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeTtsClient(byte[] returnBytes) : ITtsClient
    {
        public Task<byte[]> SynthesizeAsync(string text, string language, string voiceGender, CancellationToken ct = default)
            => Task.FromResult(returnBytes);
    }

    private sealed class SpyTtsClient : ITtsClient
    {
        public bool WasCalled { get; private set; }
        public string? LastLanguage { get; private set; }
        public string? LastVoiceGender { get; private set; }

        public Task<byte[]> SynthesizeAsync(string text, string language, string voiceGender, CancellationToken ct = default)
        {
            WasCalled = true;
            LastLanguage = language;
            LastVoiceGender = voiceGender;
            return Task.FromResult(new byte[] { 0x01 });
        }
    }

    private sealed class FakeAudioStorage : IAudioStorage
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public Task WriteAsync(string key, byte[] data, CancellationToken ct = default) { _store[key] = data; return Task.CompletedTask; }
        public Task<byte[]?> ReadAsync(string key, CancellationToken ct = default) => Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);
        public Task DeleteAsync(string key, CancellationToken ct = default) { _store.Remove(key); return Task.CompletedTask; }
    }

    private sealed class AudioTestDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
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
