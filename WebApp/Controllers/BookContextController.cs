using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Services;

namespace WebApp.Controllers;

[ApiController]
[Authorize]
[Route("api/books/{bookId:guid}/context")]
public class BookContextController(IBookContextService bookContextService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid bookId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var context = await bookContextService.GetContextAsync(bookId, userId);

        if (context is null)
            return NotFound(new { message = "No context found for this book." });

        return Ok(new { context });
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid bookId, [FromBody] GenerateBookContextToolRequest? request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            var result = await bookContextService.GenerateToolResponseAsync(bookId, userId, request?.Context, ct);

            return Ok(new GenerateBookContextToolResponse(
                ToolName: "GenerateBookContext",
                BookId: result.BookId,
                BookTitle: result.BookTitle,
                BookAuthor: result.BookAuthor,
                GeneratedContext: result.GeneratedContext,
                AppendedContext: result.AppendedContext));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update(Guid bookId, [FromBody] UpdateContextRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            var context = await bookContextService.SaveManualAsync(bookId, userId, request.Context);
            return Ok(new { context });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(Guid bookId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            await bookContextService.ClearAsync(bookId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

public record UpdateContextRequest(string Context);
public record GenerateBookContextToolRequest(string? Context);
public record GenerateBookContextToolResponse(
    string ToolName,
    Guid BookId,
    string BookTitle,
    string BookAuthor,
    string GeneratedContext,
    string AppendedContext);
