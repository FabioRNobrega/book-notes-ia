namespace WebApp.Services;

public sealed record GenerateBookContextToolResult(
    Guid BookId,
    string BookTitle,
    string BookAuthor,
    string GeneratedContext,
    string AppendedContext);
