using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class ChatClientProviderTests
{
    [Fact]
    public void GetChatClient_ResolvesKeyedClient()
    {
        var free = new FakeChatClient("free");
        var premium = new FakeChatClient("premium");
        var services = new ServiceCollection()
            .AddKeyedSingleton<IChatClient>("free", free)
            .AddKeyedSingleton<IChatClient>("premium", premium)
            .BuildServiceProvider();

        var provider = new ChatClientProvider(services);

        Assert.Same(free, provider.GetChatClient("free"));
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
