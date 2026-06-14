using TtsService.Api.Models;

namespace TtsService.Api.Services;

public interface ITtsService
{
    Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken);
}
