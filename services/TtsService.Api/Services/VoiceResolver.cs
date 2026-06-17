using Microsoft.Extensions.Options;
using TtsService.Api.Options;

namespace TtsService.Api.Services;

public sealed class VoiceResolver(IOptions<TtsDefaultsOptions> defaults)
{
    private readonly TtsDefaultsOptions _defaults = defaults.Value;

    public string Resolve(string language, string voiceGender, string? explicitVoiceName)
    {
        if (!string.IsNullOrWhiteSpace(explicitVoiceName))
            return explicitVoiceName.Trim().ToUpperInvariant();

        var normalizedLanguage = NormalizeLanguage(language);
        var normalizedGender = voiceGender.Trim().ToLowerInvariant();

        if (!_defaults.Languages.TryGetValue(normalizedLanguage, out var voiceDefaults))
            voiceDefaults = _defaults.Languages.TryGetValue("en", out var en) ? en : new VoiceDefaults();

        return normalizedGender switch
        {
            "male" => voiceDefaults.Male,
            _ => voiceDefaults.Female
        };
    }

    public static string NormalizeLanguage(string language) =>
        language.Trim().ToLowerInvariant() switch
        {
            "pt-br" or "pt_br" or "portuguese" => "pt",
            "en-us" or "en-gb" or "english" => "en",
            "sv-se" or "sv_se" or "swedish" => "sv",
            "es-es" or "es-419" or "es_419" or "spanish" => "es",
            var v => v
        };
}
