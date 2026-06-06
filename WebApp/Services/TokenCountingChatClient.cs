using Microsoft.Extensions.AI;

namespace WebApp.Services;

public sealed class TokenAccumulator
{
    private int _totalInputTokensProcessed;
    private int _totalOutputTokensGenerated;
    private int _latestPromptTokens;
    private int _latestOutputTokens;
    private int _maxPromptTokens;
    private int _maxOutputTokens;
    private int _callCount;

    public int TotalInputTokensProcessed => Volatile.Read(ref _totalInputTokensProcessed);
    public int TotalOutputTokensGenerated => Volatile.Read(ref _totalOutputTokensGenerated);
    public int LatestPromptTokens => Volatile.Read(ref _latestPromptTokens);
    public int LatestOutputTokens => Volatile.Read(ref _latestOutputTokens);
    public int MaxPromptTokens => Volatile.Read(ref _maxPromptTokens);
    public int MaxOutputTokens => Volatile.Read(ref _maxOutputTokens);
    public int CallCount => Volatile.Read(ref _callCount);

    // Null-usage calls increment CallCount but leave token metrics at zero.
    public void Add(int inputTokens, int outputTokens)
    {
        Interlocked.Increment(ref _callCount);

        if (inputTokens > 0)
        {
            Interlocked.Add(ref _totalInputTokensProcessed, inputTokens);
            Volatile.Write(ref _latestPromptTokens, inputTokens);
            UpdateMax(ref _maxPromptTokens, inputTokens);
        }

        if (outputTokens > 0)
        {
            Interlocked.Add(ref _totalOutputTokensGenerated, outputTokens);
            Volatile.Write(ref _latestOutputTokens, outputTokens);
            UpdateMax(ref _maxOutputTokens, outputTokens);
        }
    }

    private static void UpdateMax(ref int field, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref field);
            if (value <= current) return;
        } while (Interlocked.CompareExchange(ref field, value, current) != current);
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
