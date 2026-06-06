using Microsoft.Extensions.AI;

namespace WebApp.Services;

public sealed class TokenAccumulator
{
    private int _inputTokens;
    private int _outputTokens;

    public int InputTokens => Volatile.Read(ref _inputTokens);
    public int OutputTokens => Volatile.Read(ref _outputTokens);

    public void Add(int inputTokens, int outputTokens)
    {
        if (inputTokens > 0)
            Interlocked.Add(ref _inputTokens, inputTokens);

        if (outputTokens > 0)
            Interlocked.Add(ref _outputTokens, outputTokens);
    }
}

public sealed class TokenCountingChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    private static readonly AsyncLocal<TokenAccumulator?> CurrentAccumulator = new();

    public static IDisposable BeginScope(out TokenAccumulator accumulator)
    {
        var previous = CurrentAccumulator.Value;
        accumulator = new TokenAccumulator();
        CurrentAccumulator.Value = accumulator;
        return new Scope(previous);
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        CurrentAccumulator.Value?.Add(
            ToInt32(response.Usage?.InputTokenCount ?? 0),
            ToInt32(response.Usage?.OutputTokenCount ?? 0));
        return response;
    }

    private static int ToInt32(long value) => (int)Math.Clamp(value, 0, int.MaxValue);

    private sealed class Scope(TokenAccumulator? previous) : IDisposable
    {
        public void Dispose() => CurrentAccumulator.Value = previous;
    }
}
