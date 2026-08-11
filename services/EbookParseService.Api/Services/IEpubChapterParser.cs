using EbookParseService.Api.Models;

namespace EbookParseService.Api.Services;

public interface IEpubChapterParser
{
    Task<ParsedEpubBook> ParseAsync(string fileName, CancellationToken cancellationToken = default);
}
