using Microsoft.AspNetCore.Mvc;
using TtsService.Api.Models;
using TtsService.Api.Services;

namespace TtsService.Api.Controllers;

[ApiController]
[Route("tts")]
public sealed class TtsController(ITtsService ttsService, ILogger<TtsController> logger) : ControllerBase
{
    [HttpPost("synthesize")]
    [Produces("audio/wav")]
    public async Task<IActionResult> Synthesize([FromBody] TtsRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "TTS synthesize request — text_length={TextLength} language={Language} voice_gender={VoiceGender} explicit_voice={VoiceName}",
            request.Text?.Length ?? 0,
            request.Language,
            request.VoiceGender,
            request.VoiceName ?? "(none)");

        TtsAudioResult result;
        try
        {
            result = await ttsService.SynthesizeAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "TTS synthesis failed — language={Language} voice_gender={VoiceGender}",
                request.Language, request.VoiceGender);
            return StatusCode(500, new { error = ex.Message });
        }

        logger.LogInformation(
            "TTS synthesis complete — language={Language} voice={Voice} duration={Duration:0.000}s bytes={Bytes} content_type=audio/wav",
            result.Language,
            result.VoiceName,
            result.DurationSeconds,
            result.WavBytes.Length);

        Response.Headers["X-TTS-Language"] = result.Language;
        Response.Headers["X-TTS-Voice"] = result.VoiceName;
        Response.Headers["X-TTS-Duration-Seconds"] = result.DurationSeconds.ToString("0.000");

        return File(result.WavBytes, "audio/wav", "speech.wav");
    }
}
