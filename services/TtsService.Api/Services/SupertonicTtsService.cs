using Microsoft.Extensions.Options;
using TtsService.Api.Models;
using TtsService.Api.Options;

namespace TtsService.Api.Services;

public sealed class SupertonicTtsService : ITtsService
{
    private readonly SupertonicOptions _options;
    private readonly VoiceResolver _voiceResolver;

    public SupertonicTtsService(IOptions<SupertonicOptions> options, VoiceResolver voiceResolver)
    {
        _options = options.Value;
        _voiceResolver = voiceResolver;

        // Model assets should be loaded once here for the process lifetime.
        // Wire to the official Supertonic C# ONNX example when assets are available.
    }

    public Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Text is required.", nameof(request));

        throw new NotImplementedException(
            "Wire this adapter to the official Supertonic C# ONNX example. " +
            "Set Tts:UsePlaceholder=true in configuration to use the placeholder adapter during development.");
    }
}
