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
using Microsoft.Extensions.Logging;
using WebApp.Services;

namespace WebApp.Controllers;

public record ChatEntry(string Role, string Content);

[Authorize]
public class ChatController : Controller
{
    private readonly IChatOrchestratorAgent _agent;
    private readonly ICacheHandler _cache;
    private readonly IBookContextService _bookContextService;
    private readonly IChatToolRouter _toolRouter;
    private readonly ILogger<ChatController> _logger;
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static readonly TimeSpan SessionTtl = TimeSpan.FromDays(7);

    public ChatController(
        IChatOrchestratorAgent agent,
        ICacheHandler cache,
        IBookContextService bookContextService,
        IChatToolRouter toolRouter,
        ILogger<ChatController> logger)
    {
        _agent = agent;
        _cache = cache;
        _bookContextService = bookContextService;
        _toolRouter = toolRouter;
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
            var sessionJson = await _cache.GetAsync(sessionKey, ct);
            var workingContext = await _cache.GetAsync(contextKey, ct);
            var userProfileJson = await _cache.GetAsync(userProfileKey, ct);
            var profileInstructions = BuildProfileInstructions(userProfileJson);
            var routeDecision = await _toolRouter.RouteAsync(userId, message, ct);

            if (routeDecision.Tool == "GenerateBookContext" && routeDecision.BookId is Guid bookId)
            {
                var toolResult = await _bookContextService.GenerateToolResponseAsync(bookId, userId, workingContext, ct);
                workingContext = toolResult.AppendedContext;
                await _cache.SetAsync(contextKey, workingContext, SessionTtl, ct);
            }

            var runResult = await _agent.RunAsync(
                message,
                sessionJson,
                BuildOrchestratorInstructions(profileInstructions, workingContext),
                ct);

            await _cache.SetAsync(sessionKey, runResult.SerializedSessionJson, SessionTtl, ct);

            var html = Markdown.ToHtml(runResult.ResponseText, MarkdownPipeline);

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
