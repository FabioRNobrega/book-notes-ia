using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Controllers;

public record ChatEntry(string Role, string Content);
internal sealed record OrchestratorRouteDecision(string Tool, Guid? BookId);
internal sealed record BookRoutingCandidate(Guid Id, string Title, string Author);

[Authorize]
public class ChatController : Controller
{
    private readonly AIAgent _agent;
    private readonly AppDbContext _db;
    private readonly ICacheHandler _cache;
    private readonly IBookContextService _bookContextService;
    private readonly IChatClient _chatClient;
    private readonly ILogger<ChatController> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static readonly TimeSpan SessionTtl = TimeSpan.FromDays(7);

    public ChatController(
        AIAgent agent,
        AppDbContext db,
        ICacheHandler cache,
        IBookContextService bookContextService,
        IChatClient chatClient,
        ILogger<ChatController> logger)
    {
        _agent = agent;
        _db = db;
        _cache = cache;
        _bookContextService = bookContextService;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<IActionResult> Chat(CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return PartialView("Chat", new List<ChatEntry>());

        var history = new List<ChatEntry>();

        var sessionJson = await _cache.GetAsync($"agentsession:{userId}", ct);
        if (!string.IsNullOrWhiteSpace(sessionJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(sessionJson);

                if (doc.RootElement.TryGetProperty("chatHistoryProviderState", out var state) &&
                    state.TryGetProperty("messages", out var messages) &&
                    messages.ValueKind == JsonValueKind.Array)
                {
                    foreach (var msg in messages.EnumerateArray())
                    {
                        var role = msg.TryGetProperty("role", out var r) ? r.GetString() : null;
                        if (role is not "user" and not "assistant") continue;

                        var text = "";
                        if (msg.TryGetProperty("contents", out var contents) &&
                            contents.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var part in contents.EnumerateArray())
                            {
                                if (part.TryGetProperty("$type", out var t) && t.GetString() == "text" &&
                                    part.TryGetProperty("text", out var textProp))
                                    text += textProp.GetString();
                            }
                        }

                        if (string.IsNullOrWhiteSpace(text)) continue;

                        var content = role == "assistant" ? Markdown.ToHtml(text, MarkdownPipeline) : text;
                        history.Add(new ChatEntry(role!, content));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse session history for user {UserId}", userId);
            }
        }

        return PartialView("Chat", history);
    }

    [HttpPost("/chat/send")]
    public async Task<IActionResult> Send([FromForm] string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Content("");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var sessionKey = $"agentsession:{userId}";
        var contextKey = $"agentcontext:{userId}";
        var userProfileKey = $"agentprofile:{userId}";

        try
        {
            var session = await LoadOrCreateSessionAsync(sessionKey, ct);
            var workingContext = await _cache.GetAsync(contextKey, ct);
            var userProfileJson = await _cache.GetAsync(userProfileKey, ct);
            var profileInstructions = BuildProfileInstructions(userProfileJson);
            var routeDecision = await RouteAsync(userId, message, ct);

            if (routeDecision.Tool == "GenerateBookContext" && routeDecision.BookId is Guid bookId)
            {
                var toolResult = await _bookContextService.GenerateToolResponseAsync(bookId, userId, workingContext, ct);
                workingContext = toolResult.AppendedContext;
                await _cache.SetAsync(contextKey, workingContext, SessionTtl, ct);
            }

            var runOptions = new ChatClientAgentRunOptions
            {
                ChatOptions = new ChatOptions
                {
                    Instructions = BuildOrchestratorInstructions(profileInstructions, workingContext)
                }
            };

            var response = await _agent.RunAsync(message, session, runOptions, ct);

            var serialized = await _agent.SerializeSessionAsync(session, cancellationToken: ct);
            var serializedJson = serialized.GetRawText();
            await _cache.SetAsync(sessionKey, serializedJson, SessionTtl, ct);

            var html = Markdown.ToHtml(response.Text ?? string.Empty, MarkdownPipeline);

            return PartialView("_BotMessage", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat send failed for user {UserId}", userId);
            return PartialView("_BotMessage", $"⚠️ Error: {ex.Message}");
        }
    }

    [HttpPost("/chat/reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var sessionKey = $"agentsession:{userId}";
        var contextKey = $"agentcontext:{userId}";

        await _cache.RemoveAsync(sessionKey, ct);
        await _cache.RemoveAsync(contextKey, ct);
        return PartialView("~/Views/Shared/Components/_Alert.cshtml",
            (true, "Chat session history has been deleted"));
    }

    private async Task<AgentSession> LoadOrCreateSessionAsync(string sessionKey, CancellationToken ct)
    {
        var sessionJson = await _cache.GetAsync(sessionKey, ct);

        if (!string.IsNullOrWhiteSpace(sessionJson))
        {
            using var doc = JsonDocument.Parse(sessionJson);
            return await _agent.DeserializeSessionAsync(doc.RootElement);
        }

        return await _agent.CreateSessionAsync(ct);
    }

    private async Task<OrchestratorRouteDecision> RouteAsync(string userId, string message, CancellationToken ct)
    {
        var books = await _db.Books
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new BookRoutingCandidate(x.Id, x.Title, x.Author))
            .Take(25)
            .ToListAsync(ct);

        if (books.Count == 0)
            return new OrchestratorRouteDecision("none", null);

        var heuristicBookId = TryRouteGenerateBookContext(message, books);
        if (heuristicBookId is not null)
            return new OrchestratorRouteDecision("GenerateBookContext", heuristicBookId);

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
            var response = await _chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, routingPrompt)],
                cancellationToken: ct);

            var parsed = ParseRouteDecision(response.Text);
            return parsed ?? new OrchestratorRouteDecision("none", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to route tool invocation for user {UserId}", userId);
            return new OrchestratorRouteDecision("none", null);
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

    private static OrchestratorRouteDecision? ParseRouteDecision(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        var json = ExtractJsonObject(responseText);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var decision = JsonSerializer.Deserialize<OrchestratorRouteDecision>(json, JsonOptions);
            return decision is null
                ? null
                : new OrchestratorRouteDecision(
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

    private static string? BuildOrchestratorInstructions(string? profileInstructions, string? workingContext)
    {
        var sections = new List<string>
        {
            """
            You are the orchestrator for the Book Notes IA chat experience.
            Use any supplied working context as authoritative reference material for the current conversation.
            If working context is missing relevant facts, be honest about that instead of inventing details.
            Prefer grounded answers that explicitly use the user's saved books and notes context when available.
            """
        };

        if (!string.IsNullOrWhiteSpace(profileInstructions))
            sections.Add(profileInstructions);

        if (!string.IsNullOrWhiteSpace(workingContext))
        {
            sections.Add(
                $"""
                Working context gathered from tools:
                {workingContext}
                """);
        }

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private static string? BuildProfileInstructions(string? userProfileJson)
    {
        // No profile => no extra instructions for this invocation.
        if (string.IsNullOrWhiteSpace(userProfileJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(userProfileJson);
            var root = doc.RootElement;

            static string? GetString(JsonElement e, string name)
            {
                return e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetString()
                    : null;
            }

            static string JoinArray(JsonElement e, string name)
            {
                if (!e.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
                    return "";

                var items = p.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

                return items.Length == 0 ? "" : string.Join(", ", items!);
            }

            var nickname = GetString(root, "nickname");
            var preferredLanguage = GetString(root, "preferred_language");
            var tone = GetString(root, "tone");
            var learningGoals = GetString(root, "learning_goals");
            var favoriteAuthors = GetString(root, "favorite_authors");
            var aboutMe = GetString(root, "about_me");

            var readingLanguages = JoinArray(root, "reading_languages");
            var learningStyle = JoinArray(root, "learning_style");
            var lovedGenres = JoinArray(root, "loved_genres");
            var dislikedGenres = JoinArray(root, "disliked_genres");

            // Keep it compact but explicit.
            // This is per-invocation, so avoid excessive tokens.
            return
                $"""
                User profile (authoritative):
                - Name/nickname: {nickname ?? "unknown"}
                - Preferred language: {preferredLanguage ?? "not set"} (reply in this language by default)
                - Tone preference: {tone ?? "not set"}
                - Learning goals: {learningGoals ?? "not set"}
                - Favorite authors: {favoriteAuthors ?? "not set"}
                - About: {aboutMe ?? "not set"}
                - Reading languages: {(string.IsNullOrWhiteSpace(readingLanguages) ? "not set" : readingLanguages)}
                - Learning style: {(string.IsNullOrWhiteSpace(learningStyle) ? "not set" : learningStyle)}
                - Loved genres: {(string.IsNullOrWhiteSpace(lovedGenres) ? "not set" : lovedGenres)}
                - Disliked genres: {(string.IsNullOrWhiteSpace(dislikedGenres) ? "not set" : dislikedGenres)}

                Behavior rules:
                - Respect preferred language and tone.
                - When recommending books, prioritize loved genres and favorite authors.
                - Avoid disliked genres unless the user explicitly asks for them.
                - If the request is ambiguous, ask at most one short clarifying question.
                """;
        }
        catch
        {
            return null;
        }
    }
}
