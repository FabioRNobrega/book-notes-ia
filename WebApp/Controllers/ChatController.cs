using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace WebApp.Controllers
{
    public class ChatController(Kernel kernel) : Controller
    {
        private readonly Kernel _kernel = kernel;

        public IActionResult Chat()
        {
            return PartialView("Chat");
        }

        [HttpPost("/chat/send")]
        public async Task<IActionResult> Send([FromForm] string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return Content("");

            try
            {
                // ✅ Get the chat completion service from the new API
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();

                // Create (or reuse) a chat history
                var chatHistory = new ChatHistory();
                chatHistory.AddUserMessage(message);

                // Ask Ollama (Gemma 3)
                var reply = await chatService.GetChatMessageContentAsync(chatHistory);

                // Render result for HTMX partial view
                return PartialView("_BotMessage", reply.Content);
            }
            catch (Exception ex)
            {
                return PartialView("_BotMessage", $"⚠️ Error: {ex.Message}");
            }
        }

        // Optional: health check endpoint to verify connection
        [HttpGet("/chat/health")]
        public async Task<IActionResult> Health()
        {
            try
            {
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();
                var test = await chatService.GetChatMessageContentAsync("Say hello from Ollama");
                return Ok(new { success = true, message = test.Content });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}
