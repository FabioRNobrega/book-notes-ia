using Microsoft.Extensions.Options;
using TtsService.Api.Options;
using TtsService.Api.Services;

namespace TtsService.Tests;

public class VoiceResolverTests
{
    private static VoiceResolver CreateResolver() =>
        new(Options.Create(new TtsDefaultsOptions
        {
            Languages = new Dictionary<string, VoiceDefaults>
            {
                ["en"] = new VoiceDefaults { Female = "F3", Male = "M3" },
                ["pt"] = new VoiceDefaults { Female = "F3", Male = "M3" },
                ["sv"] = new VoiceDefaults { Female = "F3", Male = "M3" },
                ["es"] = new VoiceDefaults { Female = "F3", Male = "M3" }
            }
        }));

    [Theory]
    [InlineData("en", "F3")]
    [InlineData("en-us", "F3")]
    [InlineData("en-gb", "F3")]
    [InlineData("english", "F3")]
    [InlineData("pt", "F3")]
    [InlineData("pt-br", "F3")]
    [InlineData("pt_br", "F3")]
    [InlineData("portuguese", "F3")]
    [InlineData("sv", "F3")]
    [InlineData("sv-se", "F3")]
    [InlineData("sv_se", "F3")]
    [InlineData("swedish", "F3")]
    [InlineData("es", "F3")]
    [InlineData("es-es", "F3")]
    [InlineData("es-419", "F3")]
    [InlineData("spanish", "F3")]
    public void Resolve_Female_ReturnsFemaleVoice(string language, string expected)
    {
        var result = CreateResolver().Resolve(language, "female", null);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("en", "M3")]
    [InlineData("en-us", "M3")]
    [InlineData("pt", "M3")]
    [InlineData("pt-br", "M3")]
    [InlineData("portuguese", "M3")]
    [InlineData("sv", "M3")]
    [InlineData("sv-se", "M3")]
    [InlineData("swedish", "M3")]
    [InlineData("es", "M3")]
    [InlineData("es-es", "M3")]
    [InlineData("spanish", "M3")]
    public void Resolve_Male_ReturnsMaleVoice(string language, string expected)
    {
        var result = CreateResolver().Resolve(language, "male", null);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Resolve_UnknownGender_DefaultsToFemale()
    {
        var result = CreateResolver().Resolve("en", "other", null);
        Assert.Equal("F3", result);
    }

    [Fact]
    public void Resolve_ExplicitVoiceName_ReturnsUppercasedName()
    {
        var result = CreateResolver().Resolve("en", "female", "f3");
        Assert.Equal("F3", result);
    }

    [Fact]
    public void Resolve_UnknownLanguage_FallsBackToEnglishDefaults()
    {
        var result = CreateResolver().Resolve("fr", "female", null);
        Assert.Equal("F3", result);
    }

    [Theory]
    [InlineData("pt-br", "pt")]
    [InlineData("pt_br", "pt")]
    [InlineData("portuguese", "pt")]
    [InlineData("en-us", "en")]
    [InlineData("en-gb", "en")]
    [InlineData("english", "en")]
    [InlineData("sv-se", "sv")]
    [InlineData("sv_se", "sv")]
    [InlineData("swedish", "sv")]
    [InlineData("es-es", "es")]
    [InlineData("es-419", "es")]
    [InlineData("es_419", "es")]
    [InlineData("spanish", "es")]
    [InlineData("fr", "fr")]
    public void NormalizeLanguage_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, VoiceResolver.NormalizeLanguage(input));
    }
}
