using Microsoft.Extensions.AI;

namespace WebApp.Services;

public sealed class ChatCompletionService(IChatClientProvider chatClientProvider) : IChatCompletionService
{
    public async Task<string> CompleteAsync(string prompt, string agentKey, CancellationToken ct = default)
    {
        var chatClient = chatClientProvider.GetChatClient(agentKey);
        var messages = new[] { new ChatMessage(ChatRole.User, prompt) };
        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        return response.Text ?? string.Empty;
    }
}
