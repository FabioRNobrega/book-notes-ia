using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using WebApp.Models;

namespace WebApp.Services;

public sealed class ChatMessageAudioService(
    AppDbContext db,
    ITtsClient ttsClient,
    IAudioStorage audioStorage,
    ILogger<ChatMessageAudioService> logger) : IChatMessageAudioService
{
    public async Task<(byte[] WavBytes, string ContentType)?> GetOrCreateAudioAsync(
        string userId, Guid messageId, CancellationToken ct = default)
    {
        var message = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.Id == messageId && m.UserId == userId && m.Role == "assistant")
            .FirstOrDefaultAsync(ct);

        if (message is null)
            return null;

        var profile = await db.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .FirstOrDefaultAsync(ct);

        var language = NormalizeLanguage(profile?.PreferredLanguage ?? "en");
        var voiceGender = NormalizeVoiceGender(profile?.VoicePreference ?? "female");
        var voice = ResolveVoiceName(voiceGender);

        var existing = await db.ChatMessageAudios
            .AsNoTracking()
            .Where(a => a.ChatMessageId == messageId && a.Language == language && a.Voice == voice)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            var cached = await audioStorage.ReadAsync(existing.StorageKey, ct);
            if (cached is not null)
                return (cached, existing.ContentType);

            logger.LogWarning(
                "Audio metadata found for message {MessageId} but storage key {Key} is missing; regenerating.",
                messageId, existing.StorageKey);
        }

        var wavBytes = await ttsClient.SynthesizeAsync(message.Content, language, voiceGender, ct);

        var storageKey = $"audio/{messageId:N}_{language}_{voice}.wav";
        await audioStorage.WriteAsync(storageKey, wavBytes, ct);

        var audio = new ChatMessageAudio
        {
            ChatMessageId = messageId,
            Language = language,
            Voice = voice,
            StorageKey = storageKey,
            ContentType = "audio/wav",
            ByteLength = wavBytes.Length,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            db.ChatMessageAudios.Add(audio);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Another concurrent request already inserted. Remove our orphan and use the winner's file.
            db.Entry(audio).State = EntityState.Detached;
            await audioStorage.DeleteAsync(storageKey, ct);

            var winner = await db.ChatMessageAudios
                .AsNoTracking()
                .Where(a => a.ChatMessageId == messageId && a.Language == language && a.Voice == voice)
                .FirstOrDefaultAsync(ct);

            if (winner is not null)
            {
                var winnerBytes = await audioStorage.ReadAsync(winner.StorageKey, ct);
                if (winnerBytes is not null)
                    return (winnerBytes, winner.ContentType);
            }
        }

        return (wavBytes, "audio/wav");
    }

    public static string NormalizeLanguage(string language) =>
        language.Trim().ToLowerInvariant() switch
        {
            "pt-br" or "pt_br" or "portuguese" => "pt",
            "en-us" or "en-gb" or "english" => "en",
            var v => v
        };

    public static string NormalizeVoiceGender(string voicePreference) =>
        voicePreference.Trim().ToLowerInvariant() switch
        {
            "male" => "male",
            _ => "female"
        };

    public static string ResolveVoiceName(string voiceGender) =>
        voiceGender == "male" ? "M3" : "F3";

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == "23505";
}
