using System.Net.Http.Json;

namespace WebApp.Services;

public sealed class TtsClient(HttpClient httpClient) : ITtsClient
{
    public async Task<byte[]> SynthesizeAsync(string text, string language, string voiceGender, CancellationToken ct = default)
    {
        var payload = new { text, language, voiceGender };

        using var response = await httpClient.PostAsJsonAsync("/tts/synthesize", payload, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
