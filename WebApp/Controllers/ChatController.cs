using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using WebApp.Services;

namespace WebApp.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly AIAgent _agent;
    private readonly ICacheHandler _cache;
    private readonly ILogger<ChatController> _logger;

    public ChatController(AIAgent agent, ICacheHandler cache, ILogger<ChatController> logger)
    {
        _agent = agent;
        _cache = cache;
        _logger = logger;
    }

    public IActionResult Chat() => PartialView("Chat");

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
        var sessionTtl = TimeSpan.FromDays(7);

        try
        {
            // 1) Load or create session
            AgentSession session;
            var sessionJson = await _cache.GetAsync(sessionKey, ct);

            if (!string.IsNullOrWhiteSpace(sessionJson))
            {
                using var doc = JsonDocument.Parse(sessionJson);
                session = await _agent.DeserializeSessionAsync(doc.RootElement);
            }
            else
            {
                session = await _agent.CreateSessionAsync(ct);
            }

            // 2) Load user profile and build per-invocation instructions
            var userProfileJson = await _cache.GetAsync(userProfileKey, ct);
            var profileInstructions = BuildProfileInstructions(userProfileJson);

            // 3) Run message with per-invocation instructions
            var runOptions = new ChatClientAgentRunOptions
            {
                ChatOptions = new ChatOptions
                {
                    Instructions = profileInstructions
                }
            };

            var response = await _agent.RunAsync(message, session, runOptions, ct);

            // 4) Persist updated session
            var serialized = await _agent.SerializeSessionAsync(session, cancellationToken: ct);
            var serializedJson = serialized.GetRawText();
            await _cache.SetAsync(sessionKey, serializedJson, sessionTtl, ct);

            // 5) Render markdown
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var html = Markdown.ToHtml(response.Text ?? string.Empty, pipeline);

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

        await _cache.RemoveAsync(sessionKey, ct);
        return PartialView("~/Views/Shared/Components/_Alert.cshtml",
            (true, "Chat session history has been deleted"));
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