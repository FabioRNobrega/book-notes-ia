using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
using Markdig;

namespace WebApp.Controllers
{
    public class ChatController(AIAgent agent) : Controller
    {
        private readonly AIAgent _agent = agent;

        public IActionResult Chat() => PartialView("Chat");

        [HttpPost("/chat/send")]
        public async Task<IActionResult> Send([FromForm] string message, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(message))
                return Content("");

            try
            {
                var session = await _agent.CreateSessionAsync(ct);
                var response = await _agent.RunAsync(message, session, cancellationToken: ct);

                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                var html = Markdown.ToHtml(response.Text ?? string.Empty, pipeline);

                return PartialView("_BotMessage", html);
            }
            catch (Exception ex)
            {
                return PartialView("_BotMessage", $"⚠️ Error: {ex.Message}");
            }
        }

        [HttpGet("/chat/health")]
        public async Task<IActionResult> Health(CancellationToken ct)
        {
            try
            {
                var session = await _agent.CreateSessionAsync(ct);
                var response = await _agent.RunAsync("Say hello from Ollama", session, cancellationToken: ct);

                return Ok(new { success = true, message = response.Text });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}
