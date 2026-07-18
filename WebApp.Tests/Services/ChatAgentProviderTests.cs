using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class ChatAgentProviderTests
{
    [Fact]
    public void GetAgent_ResolvesKeyedAgent()
    {
        var free = new ChatClientAgent(new FakeChatClient(), name: "FreeAgent");
        var premium = new ChatClientAgent(new FakeChatClient(), name: "PremiumAgent");
        var services = new ServiceCollection()
            .AddKeyedSingleton<AIAgent>("free", free)
            .AddKeyedSingleton<AIAgent>("premium", premium)
            .BuildServiceProvider();

        var provider = new ChatAgentProvider(services);

        Assert.Same(free, provider.GetAgent("free"));
        Assert.Same(premium, provider.GetAgent("premium"));
    }

    [Fact]
    public void GetAgent_WithUnknownKey_Throws()
    {
        var provider = new ChatAgentProvider(new ServiceCollection().BuildServiceProvider());

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetAgent("unknown"));
        Assert.Contains("AIAgent", ex.Message);
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

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
