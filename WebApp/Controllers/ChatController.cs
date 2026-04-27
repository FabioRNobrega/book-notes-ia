using System.Security.Claims;
using System.Text.Json;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using WebApp.Services;

namespace WebApp.Controllers;

public record ChatEntry(string Role, string Content);

[Authorize]
public class ChatController : Controller
{
    private readonly IChatOrchestratorAgent _agent;
    private readonly ICacheHandler _cache;
    private readonly IBookContextAgentTool _bookContextTool;
    private readonly AppDbContext _db;
    private readonly ILogger<ChatController> _logger;
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static readonly TimeSpan SessionTtl = TimeSpan.FromDays(7);

    public ChatController(
        IChatOrchestratorAgent agent,
        ICacheHandler cache,
        IBookContextAgentTool bookContextTool,
        AppDbContext db,
        ILogger<ChatController> logger)
    {
        _agent = agent;
        _cache = cache;
        _bookContextTool = bookContextTool;
        _db = db;
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
        var userProfileKey = $"agentprofile:{userId}";

        try
        {
            var sessionJson = await _cache.GetAsync(sessionKey, ct);
            var userProfileJson = await _cache.GetAsync(userProfileKey, ct);

            var books = await _db.Books
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.UpdatedAt)
                .Select(b => new { b.Title, b.Author })
                .Take(25)
                .ToListAsync(ct);

            var bookTitles = books.Select(b => $"{b.Title} by {b.Author}").ToList();
            var profileInstructions = BuildProfileInstructions(userProfileJson);

            IReadOnlyList<AITool>? tools = books.Count > 0
                ? [_bookContextTool.Create(userId)]
                : null;

            var runResult = await _agent.RunAsync(
                message,
                sessionJson,
                BuildOrchestratorInstructions(profileInstructions, bookTitles),
                tools,
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

        await _cache.RemoveAsync($"agentsession:{userId}", ct);
        return PartialView("~/Views/Shared/Components/_Alert.cshtml",
            (true, "Chat session history has been deleted"));
    }

    private static string BuildOrchestratorInstructions(string? profileInstructions, IReadOnlyList<string> bookTitles)
    {
        var sections = new List<string>
        {
            """
            You are the orchestrator for the Book Notes IA chat experience.
            When the user asks about a book in their library, use the GenerateBookContext tool to retrieve its context.
            If context is missing relevant facts, be honest about that instead of inventing details.
            Prefer grounded answers that explicitly use the user's saved books and notes context when available.
            """
        };

        if (!string.IsNullOrWhiteSpace(profileInstructions))
            sections.Add(profileInstructions);

        if (bookTitles.Count > 0)
        {
            sections.Add(
                $"""
                User's book library ({bookTitles.Count} books):
                {string.Join(Environment.NewLine, bookTitles.Select(t => $"- {t}"))}

                When the user asks about one of these books, call the GenerateBookContext tool with the book title.
                """);
        }

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private static string? BuildProfileInstructions(string? userProfileJson)
    {
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
