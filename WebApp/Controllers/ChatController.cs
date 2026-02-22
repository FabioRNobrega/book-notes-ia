using System.Security.Claims;
using System.Text.Json;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
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

            // 2) Run message
            var response = await _agent.RunAsync(message, session, cancellationToken: ct);

            // 3) Persist updated session
            var serialized = await _agent.SerializeSessionAsync(session, cancellationToken: ct);
            var serializedJson = serialized.GetRawText();
            await _cache.SetAsync(sessionKey, serializedJson, sessionTtl, ct);

            // 4) Render markdown
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
}