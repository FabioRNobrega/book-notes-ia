using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class ChatOrchestratorAgentTests
{
    [Fact]
    public async Task RunAsync_UsesProvidedAgentAndRoundTripsSessionAcrossAgents()
    {
        var freeClient = new RecordingChatClient("free response");
        var premiumClient = new RecordingChatClient("premium response");
        var freeAgent = new ChatClientAgent(freeClient, name: "FreeAgent");
        var premiumAgent = new ChatClientAgent(premiumClient, name: "PremiumAgent");
        var orchestrator = new ChatOrchestratorAgent();

        var first = await orchestrator.RunAsync(freeAgent, "first question", null, "be brief", null, CancellationToken.None);
        var second = await orchestrator.RunAsync(premiumAgent, "second question", first.SerializedSessionJson, "be brief", null, CancellationToken.None);

        Assert.Equal("free response", first.ResponseText);
        Assert.Equal("premium response", second.ResponseText);
        Assert.Contains(premiumClient.LastMessages, message => string.Equals(message.Text, "first question", StringComparison.Ordinal));
        Assert.Contains(premiumClient.LastMessages, message => string.Equals(message.Text, "second question", StringComparison.Ordinal));
    }

    private sealed class RecordingChatClient(string responseText) : IChatClient
    {
        public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        }

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
