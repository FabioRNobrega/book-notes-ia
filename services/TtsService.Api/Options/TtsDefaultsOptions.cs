namespace TtsService.Api.Options;

public sealed class TtsDefaultsOptions
{
    public Dictionary<string, VoiceDefaults> Languages { get; init; } = new();
}

public sealed class VoiceDefaults
{
    public string Female { get; init; } = "F3";
    public string Male { get; init; } = "M3";
}
