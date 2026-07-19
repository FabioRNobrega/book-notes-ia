using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class ChatClientProviderTests
{
    [Fact]
    public void GetChatClient_ResolvesKeyedClient()
    {
        var qwen = new FakeChatClient("free-qwen");
        var llama3 = new FakeChatClient("free-llama3");
        var phi4 = new FakeChatClient("free-phi4");
        var granite4 = new FakeChatClient("free-granite4");
        var premium = new FakeChatClient("premium");
        var services = new ServiceCollection()
            .AddKeyedSingleton<IChatClient>("free-qwen", qwen)
            .AddKeyedSingleton<IChatClient>("free-llama3", llama3)
            .AddKeyedSingleton<IChatClient>("free-phi4", phi4)
            .AddKeyedSingleton<IChatClient>("free-granite4", granite4)
            .AddKeyedSingleton<IChatClient>("premium", premium)
            .BuildServiceProvider();

        var provider = new ChatClientProvider(services);

        Assert.Same(qwen, provider.GetChatClient("free-qwen"));
        Assert.Same(llama3, provider.GetChatClient("free-llama3"));
        Assert.Same(phi4, provider.GetChatClient("free-phi4"));
        Assert.Same(granite4, provider.GetChatClient("free-granite4"));
        Assert.Same(premium, provider.GetChatClient("premium"));
    }

    [Fact]
    public void GetChatClient_WithUnknownKey_Throws()
    {
        var provider = new ChatClientProvider(new ServiceCollection().BuildServiceProvider());

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetChatClient("unknown"));
        Assert.Contains("IChatClient", ex.Message);
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
