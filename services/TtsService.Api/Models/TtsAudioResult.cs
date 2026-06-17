namespace TtsService.Api.Models;

public sealed class TtsAudioResult
{
    public required byte[] WavBytes { get; init; }
    public required string Language { get; init; }
    public required string VoiceName { get; init; }
    public required double DurationSeconds { get; init; }
}
