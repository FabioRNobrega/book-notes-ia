using EbookParseService.Api.Models;
using EbookParseService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EbookParseService.Api.Controllers;

[ApiController]
[Route("api/epubs")]
public sealed class EpubController(
    IEpubChapterParser parser,
    IChapterOutputWriter writer,
    ILogger<EpubController> logger) : ControllerBase
{
    [HttpGet("/health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() => Ok(new { status = "ready" });

    [HttpPost("parse")]
    [ProducesResponseType<ParseEpubResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ParseEpubResponse>> Parse(
        [FromBody] ParseEpubRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var book = await parser.ParseAsync(request.FileName, cancellationToken);
            return Ok(await writer.PublishAsync(book, cancellationToken));
        }
        catch (EpubParseException exception)
        {
            var (status, title, type) = MapError(exception.Kind);
            logger.LogWarning("EPUB parse request failed with category {ErrorKind}.", exception.Kind);
            return new ObjectResult(new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = type,
                Detail = exception.PublicMessage
            })
            {
                StatusCode = status
            };
        }
    }

    private static (int Status, string Title, string Type) MapError(EpubParseErrorKind kind) => kind switch
    {
        EpubParseErrorKind.InvalidRequest => (400, "Invalid EPUB request", "https://book-notes.local/problems/invalid-epub-request"),
        EpubParseErrorKind.OutputConflict => (409, "Chapter output conflict", "https://book-notes.local/problems/chapter-output-conflict"),
        EpubParseErrorKind.UnsupportedContent => (415, "Unsupported EPUB content", "https://book-notes.local/problems/unsupported-epub-content"),
        EpubParseErrorKind.InvalidEpub => (422, "Invalid EPUB", "https://book-notes.local/problems/invalid-epub"),
        _ => (422, "Unsupported EPUB structure", "https://book-notes.local/problems/unsupported-epub-structure")
    };
}
