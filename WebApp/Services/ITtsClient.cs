namespace WebApp.Services;

public interface ITtsClient
{
    Task<byte[]> SynthesizeAsync(string text, string language, string voiceGender, CancellationToken ct = default);
}
