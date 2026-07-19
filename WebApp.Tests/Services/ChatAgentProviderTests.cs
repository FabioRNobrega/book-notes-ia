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
        var qwen = new ChatClientAgent(new FakeChatClient(), name: "FreeQwenAgent");
        var llama3 = new ChatClientAgent(new FakeChatClient(), name: "FreeLlama3Agent");
        var phi4 = new ChatClientAgent(new FakeChatClient(), name: "FreePhi4Agent");
        var granite4 = new ChatClientAgent(new FakeChatClient(), name: "FreeGranite4Agent");
        var premium = new ChatClientAgent(new FakeChatClient(), name: "PremiumAgent");
        var services = new ServiceCollection()
            .AddKeyedSingleton<AIAgent>("free-qwen", qwen)
            .AddKeyedSingleton<AIAgent>("free-llama3", llama3)
            .AddKeyedSingleton<AIAgent>("free-phi4", phi4)
            .AddKeyedSingleton<AIAgent>("free-granite4", granite4)
            .AddKeyedSingleton<AIAgent>("premium", premium)
            .BuildServiceProvider();

        var provider = new ChatAgentProvider(services);

        Assert.Same(qwen, provider.GetAgent("free-qwen"));
        Assert.Same(llama3, provider.GetAgent("free-llama3"));
        Assert.Same(phi4, provider.GetAgent("free-phi4"));
        Assert.Same(granite4, provider.GetAgent("free-granite4"));
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
