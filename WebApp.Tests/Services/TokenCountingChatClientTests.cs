using Microsoft.Extensions.AI;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class TokenCountingChatClientTests
{
    [Fact]
    public async Task TokenCountingChatClient_AccumulatesAcrossMultipleCalls()
    {
        using var client = new TokenCountingChatClient(new FakeChatClient(100, 50));
        using var scope = TokenCountingChatClient.BeginScope(out var accumulator);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "again")]);

        Assert.Equal(200, accumulator.InputTokens);
        Assert.Equal(100, accumulator.OutputTokens);
    }

    [Fact]
    public async Task TokenCountingChatClient_ScopesAreIndependent()
    {
        using var client = new TokenCountingChatClient(new FakeChatClient(100, 50));

        using (TokenCountingChatClient.BeginScope(out var first))
        {
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);
            Assert.Equal(100, first.InputTokens);
        }

        using (TokenCountingChatClient.BeginScope(out var second))
        {
            Assert.Equal(0, second.InputTokens);
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "again")]);
            Assert.Equal(100, second.InputTokens);
        }
    }

    [Fact]
    public async Task TokenCountingChatClient_HandlesNullUsageGracefully()
    {
        using var client = new TokenCountingChatClient(new FakeChatClient(null, null));
        using var scope = TokenCountingChatClient.BeginScope(out var accumulator);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.Equal(0, accumulator.InputTokens);
        Assert.Equal(0, accumulator.OutputTokens);
    }

    private sealed class FakeChatClient(int? inputTokens, int? outputTokens) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"));
            if (inputTokens is not null || outputTokens is not null)
            {
                response.Usage = new UsageDetails
                {
                    InputTokenCount = inputTokens ?? 0,
                    OutputTokenCount = outputTokens ?? 0
                };
            }

            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            EmptyStreamingResponse();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }

        private static async IAsyncEnumerable<ChatResponseUpdate> EmptyStreamingResponse()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
