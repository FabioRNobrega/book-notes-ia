using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace WebApp.Services;

public interface IChatToolRouter
{
    Task<ChatToolRouteDecision> RouteAsync(string userId, string message, CancellationToken ct = default);
}

internal sealed record BookRoutingCandidate(Guid Id, string Title, string Author);

public sealed class ChatToolRouter(
    AppDbContext db,
    IChatClient chatClient,
    ILogger<ChatToolRouter> logger) : IChatToolRouter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ChatToolRouteDecision> RouteAsync(string userId, string message, CancellationToken ct = default)
    {
        var books = await db.Books
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new BookRoutingCandidate(x.Id, x.Title, x.Author))
            .Take(25)
            .ToListAsync(ct);

        if (books.Count == 0)
            return new ChatToolRouteDecision("none", null);

        var heuristicBookId = TryRouteGenerateBookContext(message, books);
        if (heuristicBookId is not null)
            return new ChatToolRouteDecision("GenerateBookContext", heuristicBookId);

        var booksList = string.Join(Environment.NewLine, books.Select(book => $"- {book.Id} | {book.Title} | {book.Author}"));
        var routingPrompt = $$"""
            You are the orchestration router for a book notes assistant.
            Decide whether to call a tool before the assistant answers.

            Available tool:
            - GenerateBookContext(bookId): Use when the user explicitly asks to generate, create, regenerate, or provide book context/background for one specific book.

            Rules:
            - Return JSON only.
            - Use the tool only for one clear matching book from the available books list.
            - If the request is ambiguous, references multiple books, or is not clearly about generating book context, return tool "none".
            - Never invent a book id.

            Response schema:
            {"tool":"none","bookId":null}
            or
            {"tool":"GenerateBookContext","bookId":"GUID"}

            User message:
            {{message}}

            Available books:
            {{booksList}}
            """;

        try
        {
            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, routingPrompt)],
                cancellationToken: ct);

            var parsed = ParseRouteDecision(response.Text);
            return parsed ?? new ChatToolRouteDecision("none", null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to route tool invocation for user {UserId}", userId);
            return new ChatToolRouteDecision("none", null);
        }
    }

    private static Guid? TryRouteGenerateBookContext(string message, IReadOnlyList<BookRoutingCandidate> books)
    {
        if (!IsBookContextRequest(message))
            return null;

        if (Guid.TryParse(message.Trim(), out var directBookId))
            return books.Any(book => book.Id == directBookId) ? directBookId : null;

        foreach (var book in books)
        {
            if (ContainsBookReference(message, book))
                return book.Id;
        }

        return null;
    }

    private static bool IsBookContextRequest(string message)
    {
        var normalized = Normalize(message);
        return normalized.Contains("context")
            || normalized.Contains("background")
            || normalized.Contains("historical")
            || normalized.Contains("literarymovement")
            || normalized.Contains("aboutthisbook");
    }

    private static bool ContainsBookReference(string message, BookRoutingCandidate book)
    {
        var normalizedMessage = Normalize(message);
        var normalizedTitle = Normalize(book.Title);
        var normalizedAuthor = Normalize(book.Author);

        return normalizedMessage.Contains(normalizedTitle) || normalizedMessage.Contains(normalizedAuthor);
    }

    private static string Normalize(string value)
        => new(value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static ChatToolRouteDecision? ParseRouteDecision(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        var json = ExtractJsonObject(responseText);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var decision = JsonSerializer.Deserialize<ChatToolRouteDecision>(json, JsonOptions);
            return decision is null
                ? null
                : new ChatToolRouteDecision(
                    string.IsNullOrWhiteSpace(decision.Tool) ? "none" : decision.Tool,
                    decision.BookId);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractJsonObject(string responseText)
    {
        var start = responseText.IndexOf('{');
        var end = responseText.LastIndexOf('}');

        if (start < 0 || end <= start)
            return null;

        return responseText[start..(end + 1)];
    }
}
