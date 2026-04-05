using Microsoft.Extensions.AI;

namespace WebApp.Services;

public class OllamaService(IChatClient chatClient) : IOllamaService
{
    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var messages = new[] { new ChatMessage(ChatRole.User, prompt) };
        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        return response.Text ?? string.Empty;
    }
}
