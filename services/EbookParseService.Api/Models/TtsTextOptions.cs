namespace EbookParseService.Api.Models;

public sealed record TtsTextOptions(string Language)
{
    public static bool IsSupported(string? language) => language is "en" or "pt";
}
