using EbookParseService.Api.Models;

namespace EbookParseService.Api.Services;

public interface ITtsTextNormalizer
{
    ParsedEpubBook Normalize(ParsedEpubBook book, TtsTextOptions options);
}
