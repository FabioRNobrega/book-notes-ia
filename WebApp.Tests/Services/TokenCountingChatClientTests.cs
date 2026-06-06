using Microsoft.Extensions.AI;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class TokenCountingChatClientTests
{
    [Fact]
    public async Task TokenCountingChatClient_TracksTotalsLatestMaxAndCallCount()
    {
        using var client = new TokenCountingChatClient(new FakeChatClient(100, 50));
        using var scope = TokenCountingChatClient.BeginScope(out var accumulator);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        using var client2 = new TokenCountingChatClient(new FakeChatClient(250, 30));
        await client2.GetResponseAsync([new ChatMessage(ChatRole.User, "again")]);

        Assert.Equal(350, accumulator.TotalInputTokensProcessed);
        Assert.Equal(80, accumulator.TotalOutputTokensGenerated);
        Assert.Equal(250, accumulator.LatestPromptTokens);
        Assert.Equal(30, accumulator.LatestOutputTokens);
        Assert.Equal(250, accumulator.MaxPromptTokens);
        Assert.Equal(50, accumulator.MaxOutputTokens);
        Assert.Equal(2, accumulator.CallCount);
    }

    [Fact]
    public async Task TokenCountingChatClient_ScopesAreIndependent()
    {
        using var client = new TokenCountingChatClient(new FakeChatClient(100, 50));

        using (TokenCountingChatClient.BeginScope(out var first))
        {
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);
            Assert.Equal(100, first.TotalInputTokensProcessed);
            Assert.Equal(1, first.CallCount);
        }

        using (TokenCountingChatClient.BeginScope(out var second))
        {
            Assert.Equal(0, second.TotalInputTokensProcessed);
            Assert.Equal(0, second.CallCount);
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "again")]);
            Assert.Equal(100, second.TotalInputTokensProcessed);
            Assert.Equal(1, second.CallCount);
        }
    }

    [Fact]
    public async Task TokenCountingChatClient_HandlesNullUsageGracefully()
    {
        using var client = new TokenCountingChatClient(new FakeChatClient(null, null));
        using var scope = TokenCountingChatClient.BeginScope(out var accumulator);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.Equal(0, accumulator.TotalInputTokensProcessed);
        Assert.Equal(0, accumulator.TotalOutputTokensGenerated);
        Assert.Equal(0, accumulator.LatestPromptTokens);
        Assert.Equal(0, accumulator.LatestOutputTokens);
        Assert.Equal(0, accumulator.MaxPromptTokens);
        Assert.Equal(0, accumulator.MaxOutputTokens);
        // Null-usage calls still increment CallCount — the model was invoked, just returned no usage metadata.
        Assert.Equal(1, accumulator.CallCount);
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
