using EbookParseService.Api.Models;

namespace EbookParseService.Api.Services;

public interface IChapterOutputWriter
{
    Task<ParseEpubResponse> PublishAsync(ParsedEpubBook book, CancellationToken cancellationToken = default);
}
