using Microsoft.Extensions.AI;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class ChatCompletionServiceTests
{
    [Theory]
    [InlineData("free")]
    [InlineData("premium")]
    public async Task CompleteAsync_ResolvesChatClientByAgentKey(string agentKey)
    {
        var provider = new FakeChatClientProvider();
        var service = new ChatCompletionService(provider);

        var result = await service.CompleteAsync("Say hello", agentKey, CancellationToken.None);

        Assert.Equal($"{agentKey} response", result);
        Assert.Equal(agentKey, provider.LastAgentKey);
    }

    private sealed class FakeChatClientProvider : IChatClientProvider
    {
        public string? LastAgentKey { get; private set; }

        public IChatClient GetChatClient(string agentKey)
        {
            LastAgentKey = agentKey;
            return new FakeChatClient($"{agentKey} response");
        }
    }

    private sealed class FakeChatClient(string responseText) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
