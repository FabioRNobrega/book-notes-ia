using TtsService.Api.Models;
using TtsService.Api.Options;
using Microsoft.Extensions.Options;

namespace TtsService.Api.Services;

// Deterministic development fallback — only active when Tts:UsePlaceholder=true.
// Returns a minimal valid WAV header so the browser can accept the response.
public sealed class PlaceholderTtsService(IOptions<TtsDefaultsOptions> defaults, VoiceResolver voiceResolver) : ITtsService
{
    public Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Text is required.", nameof(request));

        var language = VoiceResolver.NormalizeLanguage(request.Language);
        var voiceName = voiceResolver.Resolve(language, request.VoiceGender, request.VoiceName);

        // Return a silent 1-second WAV (PCM 16-bit, 44100 Hz, mono)
        var wavBytes = BuildSilentWav(sampleRate: 44100, durationSeconds: 1);

        return Task.FromResult(new TtsAudioResult
        {
            WavBytes = wavBytes,
            Language = language,
            VoiceName = voiceName,
            DurationSeconds = 1.0
        });
    }

    private static byte[] BuildSilentWav(int sampleRate, int durationSeconds)
    {
        int numSamples = sampleRate * durationSeconds;
        int dataSize = numSamples * 2; // 16-bit = 2 bytes per sample

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);           // chunk size
        writer.Write((short)1);     // PCM
        writer.Write((short)1);     // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2); // byte rate
        writer.Write((short)2);     // block align
        writer.Write((short)16);    // bits per sample

        // data chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]); // silence

        return ms.ToArray();
    }
}
