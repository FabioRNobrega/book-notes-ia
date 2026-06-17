namespace TtsService.Api.Options;

public sealed class SupertonicOptions
{
    public required string AssetsPath { get; init; }
    public int SampleRate { get; init; } = 44100;
    public int DefaultTotalSteps { get; init; } = 8;
    public float DefaultSpeed { get; init; } = 1.05f;
}
