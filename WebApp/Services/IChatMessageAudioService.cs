namespace WebApp.Services;

public interface IChatMessageAudioService
{
    Task<(byte[] WavBytes, string ContentType)?> GetOrCreateAudioAsync(
        string userId, Guid messageId, CancellationToken ct = default);
}
