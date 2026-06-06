using System.Security.Claims;
using System.Text.Json;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Controllers;

public record ChatEntry(string Role, string Content, long? ResponseTimeMs = null);

[Authorize]
public class ChatController : Controller
{
    private readonly IChatOrchestratorAgent _agent;
    private readonly ICacheHandler _cache;
    private readonly IBookContextAgentTool _bookContextTool;
    private readonly IBookNotesAgentTool _bookNotesTool;
    private readonly IBookNoteSearchAgentTool _bookNoteSearchTool;
    private readonly AppDbContext _db;
    private readonly ILogger<ChatController> _logger;
    private readonly int _numCtx;
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static readonly TimeSpan SessionTtl = TimeSpan.FromDays(7);
    private const string ProfileInstructionTemplate = "Reader: {0}.";

    public ChatController(
        IChatOrchestratorAgent agent,
        ICacheHandler cache,
        IBookContextAgentTool bookContextTool,
        IBookNotesAgentTool bookNotesTool,
        IBookNoteSearchAgentTool bookNoteSearchTool,
        AppDbContext db,
        ILogger<ChatController> logger,
        IConfiguration configuration)
    {
        _agent = agent;
        _cache = cache;
        _bookContextTool = bookContextTool;
        _bookNotesTool = bookNotesTool;
        _bookNoteSearchTool = bookNoteSearchTool;
        _db = db;
        _logger = logger;
        _numCtx = configuration.GetValue<int?>("Ollama:NumCtx") ?? 8192;
    }

    public async Task<IActionResult> Chat(CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return PartialView("Chat", new List<ChatEntry>());

        var sessionId = await GetCurrentSessionIdAsync(userId, ct);
        if (sessionId is null)
            return PartialView("Chat", new List<ChatEntry>());

        var messages = await _db.ChatMessages
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.SessionId == sessionId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(ct);

        var history = messages
            .Select(x => new ChatEntry(
                x.Role,
                x.Role == "assistant" ? Markdown.ToHtml(x.Content, MarkdownPipeline) : x.Content,
                x.ResponseTimeMs))
            .ToList();

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

        var activeSessionIdKey = $"activesessionid:{userId}";
        var userProfileKey = $"agentprofile:{userId}";

        try
        {
            var sessionId = await GetOrCreateSessionIdAsync(userId, activeSessionIdKey, ct);
            var sessionKey = BuildSessionKey(userId, sessionId);
            var contextKey = BuildContextKey(userId, sessionId);
            var sessionJson = await _cache.GetAsync(sessionKey, ct);
            var userProfileJson = await _cache.GetAsync(userProfileKey, ct);

            var profileInstructions = BuildProfileInstructions(userProfileJson);
            var orchestratorInstructions = BuildOrchestratorInstructions(profileInstructions);

            IReadOnlyList<AITool> tools = [_bookContextTool.Create(userId), _bookNotesTool.Create(userId), _bookNoteSearchTool.Create(userId)];

            var runResult = await _agent.RunAsync(
                message,
                sessionJson,
                orchestratorInstructions,
                tools,
                ct);

            await _cache.SetAsync(sessionKey, runResult.SerializedSessionJson, SessionTtl, ct);

            var nextDisplayOrder = (await _db.ChatMessages
                .Where(x => x.UserId == userId && x.SessionId == sessionId)
                .Select(x => (long?)x.DisplayOrder)
                .MaxAsync(ct) ?? 0) + 1;

            var contextUsagePct = ComputeUsagePct(runResult.MaxPromptTokens);
            _db.ChatMessages.AddRange(
                new WebApp.Models.ChatMessage
                {
                    UserId = userId,
                    SessionId = sessionId,
                    Role = "user",
                    Content = message,
                    DisplayOrder = nextDisplayOrder
                },
                new WebApp.Models.ChatMessage
                {
                    UserId = userId,
                    SessionId = sessionId,
                    Role = "assistant",
                    Content = runResult.ResponseText,
                    DisplayOrder = nextDisplayOrder + 1,
                    TotalInputTokensProcessed = runResult.TotalInputTokensProcessed,
                    TotalOutputTokensGenerated = runResult.TotalOutputTokensGenerated,
                    LatestPromptTokens = runResult.LatestPromptTokens,
                    MaxPromptTokens = runResult.MaxPromptTokens,
                    ContextUsagePct = contextUsagePct,
                    ModelCallCount = runResult.ModelCallCount,
                    ResponseTimeMs = runResult.ElapsedMs
                });
            await _db.SaveChangesAsync(ct);

            await _cache.SetObjectAsync(
                contextKey,
                new
                {
                    totalInputTokensProcessed = runResult.TotalInputTokensProcessed,
                    totalOutputTokensGenerated = runResult.TotalOutputTokensGenerated,
                    latestPromptTokens = runResult.LatestPromptTokens,
                    maxPromptTokens = runResult.MaxPromptTokens,
                    modelCallCount = runResult.ModelCallCount,
                    numCtx = _numCtx,
                    contextUsagePct,
                    lastResponseMs = runResult.ElapsedMs
                },
                SessionTtl,
                ct);

            _logger.LogInformation(
                "Turn stats: totalInputTokensProcessed={TotalInput} totalOutputTokensGenerated={TotalOutput} latestPromptTokens={LatestPrompt} maxPromptTokens={MaxPrompt} modelCallCount={CallCount} contextUsagePct={UsagePct}% elapsedMs={ElapsedMs} promptChars={PromptChars}",
                runResult.TotalInputTokensProcessed,
                runResult.TotalOutputTokensGenerated,
                runResult.LatestPromptTokens,
                runResult.MaxPromptTokens,
                runResult.ModelCallCount,
                contextUsagePct,
                runResult.ElapsedMs,
                orchestratorInstructions.Length + message.Length);

            var html = Markdown.ToHtml(runResult.ResponseText, MarkdownPipeline);
            return PartialView("_BotMessage", new BotMessageViewModel(html, contextUsagePct, runResult.ElapsedMs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat send failed for user {UserId}", userId);
            return PartialView("_BotMessage", new BotMessageViewModel($"<p>Error: {ex.Message}</p>", 0));
        }
    }

    [HttpPost("/chat/reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var sessionId = await GetCurrentSessionIdAsync(userId, ct);
        if (sessionId is not null)
        {
            await _cache.RemoveAsync(BuildSessionKey(userId, sessionId.Value), ct);
            await _cache.RemoveAsync(BuildContextKey(userId, sessionId.Value), ct);

            var messages = await _db.ChatMessages
                .Where(x => x.UserId == userId && x.SessionId == sessionId)
                .ToListAsync(ct);

            if (messages.Count > 0)
            {
                _db.ChatMessages.RemoveRange(messages);
                await _db.SaveChangesAsync(ct);
            }
        }

        await _cache.RemoveAsync($"activesessionid:{userId}", ct);
        return PartialView("~/Views/Shared/Components/_Alert.cshtml",
            (true, "Chat session history has been deleted"));
    }

    private static string BuildOrchestratorInstructions(string? profileInstructions)
    {
        var sections = new List<string>
        {
            """
            You are the orchestrator for the Book Notes IA chat experience.
            When the user asks about any specific book or title, call the GenerateBookContext tool before answering.
            When the user asks about personal notes, highlights, or annotations for a specific book, call the GetBookNotesWithAnalysis tool before answering.
            When the user asks a focused question about a specific topic, theme, or idea within a book's notes (e.g. "what did I highlight about power in Dune?"), call GetRelevantBookNotes instead, passing the user's question as searchQuery.
            When the user asks for both literary context and personal notes for the same book, you may call both GenerateBookContext and GetBookNotesWithAnalysis and combine their results.
            Do not say a book is missing from the library unless GenerateBookContext returns a not found result.
            The GenerateBookContext tool searches the authenticated user's full library, retrieves existing Book.Context when available, and generates and saves context when it is missing.
            The GetBookNotesWithAnalysis tool retrieves the user's highlights for a book as <note>...</note> blocks. Reason over these notes yourself to produce a thematic answer — do not list the raw notes back unless the user explicitly asks to see them.
            The GetRelevantBookNotes tool performs semantic search over the user's highlights for a specific book and returns only the most relevant notes as <note loc="...">...</note> blocks. Use the loc attribute to cite the location when relevant.
            If context is missing relevant facts, be honest about that instead of inventing details.
            Prefer grounded answers that explicitly use the user's saved books and notes context when available.
            """
        };

        if (!string.IsNullOrWhiteSpace(profileInstructions))
            sections.Add(profileInstructions);

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

            var parts = new List<string>();
            AddPart(parts, GetString(root, "nickname"));
            AddPart(parts, GetString(root, "preferred_language"), "reads in ");
            AddPart(parts, GetString(root, "tone"), "prefers ", " tone");
            AddPart(parts, GetString(root, "learning_goals"), "goals: ");
            AddPart(parts, GetString(root, "favorite_authors"), "favorite authors: ");
            AddPart(parts, GetString(root, "about_me"), "about: ");
            AddPart(parts, JoinArray(root, "reading_languages"), "reading languages: ");
            AddPart(parts, JoinArray(root, "learning_style"), "learning style: ");
            AddPart(parts, JoinArray(root, "loved_genres"), "likes ");
            AddPart(parts, JoinArray(root, "disliked_genres"), "avoids ");

            return parts.Count == 0 ? null : string.Format(ProfileInstructionTemplate, string.Join(", ", parts));
        }
        catch
        {
            return null;
        }
    }

    private async Task<Guid?> GetCurrentSessionIdAsync(string userId, CancellationToken ct)
    {
        var value = await _cache.GetAsync($"activesessionid:{userId}", ct);
        return Guid.TryParse(value, out var sessionId) ? sessionId : null;
    }

    private async Task<Guid> GetOrCreateSessionIdAsync(string userId, string sessionIdKey, CancellationToken ct)
    {
        var current = await _cache.GetAsync(sessionIdKey, ct);
        if (Guid.TryParse(current, out var sessionId))
            return sessionId;

        sessionId = Guid.NewGuid();
        await _cache.SetAsync(sessionIdKey, sessionId.ToString("D"), SessionTtl, ct);
        return sessionId;
    }

    private int ComputeUsagePct(int maxPromptTokens)
    {
        if (_numCtx <= 0)
            return 0;

        return Math.Clamp((int)Math.Round(maxPromptTokens * 100.0 / _numCtx), 0, 100);
    }

    private static string BuildSessionKey(string userId, Guid sessionId) => $"agentsession:{userId}:{sessionId:D}";

    private static string BuildContextKey(string userId, Guid sessionId) => $"agentcontext:{userId}:{sessionId:D}";

    private static void AddPart(List<string> parts, string? value, string prefix = "", string suffix = "")
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add($"{prefix}{value.Trim()}{suffix}");
    }

    public static string FormatResponseTime(long? responseTimeMs)
    {
        var value = responseTimeMs.GetValueOrDefault();
        return value <= 0 ? "0s" : $"{Math.Max(1, (int)Math.Round(value / 1000.0))}s";
    }
}
