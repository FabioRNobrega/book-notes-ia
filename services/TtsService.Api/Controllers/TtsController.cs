using Microsoft.AspNetCore.Mvc;
using TtsService.Api.Models;
using TtsService.Api.Services;

namespace TtsService.Api.Controllers;

[ApiController]
[Route("tts")]
public sealed class TtsController(ITtsService ttsService) : ControllerBase
{
    [HttpPost("synthesize")]
    [Produces("audio/wav")]
    public async Task<IActionResult> Synthesize([FromBody] TtsRequest request, CancellationToken cancellationToken)
    {
        var result = await ttsService.SynthesizeAsync(request, cancellationToken);

        Response.Headers["X-TTS-Language"] = result.Language;
        Response.Headers["X-TTS-Voice"] = result.VoiceName;
        Response.Headers["X-TTS-Duration-Seconds"] = result.DurationSeconds.ToString("0.000");

        return File(result.WavBytes, "audio/wav", "speech.wav");
    }
}
