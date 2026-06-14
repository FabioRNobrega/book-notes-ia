namespace TtsService.Api.Models;

public sealed class TtsRequest
{
    public required string Text { get; init; }
    public string Language { get; init; } = "en";
    public string VoiceGender { get; init; } = "female";
    public string? VoiceName { get; init; }
    public float? Speed { get; init; }
    public int? TotalSteps { get; init; }
}
