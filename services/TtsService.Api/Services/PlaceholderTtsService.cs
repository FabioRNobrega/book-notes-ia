using TtsService.Api.Models;
using TtsService.Api.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TtsService.Api.Services;

// Deterministic development fallback — only active when Tts:UsePlaceholder=true.
// Returns a 440 Hz tone so developers can verify end-to-end audio flow without real assets.
public sealed class PlaceholderTtsService(IOptions<TtsDefaultsOptions> defaults, VoiceResolver voiceResolver, ILogger<PlaceholderTtsService> logger) : ITtsService
{
    private const int SampleRate = 22050;
    private const double Frequency = 440.0; // A4 — audible without being harsh
    private const double Amplitude = 0.35;  // 35% to avoid clipping

    public Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Text is required.", nameof(request));

        var language = VoiceResolver.NormalizeLanguage(request.Language);
        var voiceName = voiceResolver.Resolve(language, request.VoiceGender, request.VoiceName);

        // Duration scales with text length — gives a more realistic placeholder experience.
        double durationSeconds = Math.Clamp(request.Text.Length / 15.0, 1.0, 10.0);

        logger.LogInformation(
            "[Placeholder] Generating tone — language={Language} voice={Voice} duration={Duration:0.000}s sample_rate={SampleRate}Hz",
            language, voiceName, durationSeconds, SampleRate);

        var wavBytes = BuildToneWav(SampleRate, Frequency, Amplitude, durationSeconds);

        logger.LogInformation(
            "[Placeholder] Tone generated — bytes={Bytes}", wavBytes.Length);

        return Task.FromResult(new TtsAudioResult
        {
            WavBytes = wavBytes,
            Language = language,
            VoiceName = voiceName,
            DurationSeconds = durationSeconds
        });
    }

    private static byte[] BuildToneWav(int sampleRate, double frequency, double amplitude, double durationSeconds)
    {
        int numSamples = (int)(sampleRate * durationSeconds);
        int dataSize = numSamples * 2; // 16-bit mono = 2 bytes per sample

        using var ms = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);               // chunk size
        writer.Write((short)1);         // PCM
        writer.Write((short)1);         // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);   // byte rate
        writer.Write((short)2);         // block align
        writer.Write((short)16);        // bits per sample

        // data chunk
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        short maxSample = (short)(short.MaxValue * amplitude);
        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            // Apply a short linear fade-in/out to avoid clicks at the boundaries.
            double fade = i < 100 ? i / 100.0 : i > numSamples - 100 ? (numSamples - i) / 100.0 : 1.0;
            short sample = (short)(maxSample * Math.Sin(2 * Math.PI * frequency * t) * fade);
            writer.Write(sample);
        }

        return ms.ToArray();
    }
}
