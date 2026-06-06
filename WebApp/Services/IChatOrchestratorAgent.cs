using System.Text.Json;
using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace WebApp.Services;

public sealed record ChatAgentRunResult(
    string ResponseText,
    string SerializedSessionJson,
    int TotalInputTokensProcessed,
    int TotalOutputTokensGenerated,
    int LatestPromptTokens,
    int LatestOutputTokens,
    int MaxPromptTokens,
    int MaxOutputTokens,
    int ModelCallCount,
    long ElapsedMs);

public interface IChatOrchestratorAgent
{
    Task<ChatAgentRunResult> RunAsync(string message, string? sessionJson, string? instructions, IReadOnlyList<AITool>? tools = null, CancellationToken ct = default);
}

public sealed class ChatOrchestratorAgent(AIAgent agent) : IChatOrchestratorAgent
{
    public async Task<ChatAgentRunResult> RunAsync(string message, string? sessionJson, string? instructions, IReadOnlyList<AITool>? tools = null, CancellationToken ct = default)
    {
        AgentSession session;

        if (!string.IsNullOrWhiteSpace(sessionJson))
        {
            using var doc = JsonDocument.Parse(sessionJson);
            session = await agent.DeserializeSessionAsync(doc.RootElement);
        }
        else
        {
            session = await agent.CreateSessionAsync(ct);
        }

        var runOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = instructions,
                Tools = tools?.ToList()
            }
        };

        var stopwatch = Stopwatch.StartNew();
        using var tokenScope = TokenCountingChatClient.BeginScope(out var accumulator);
        var response = await agent.RunAsync(message, session, runOptions, ct);
        stopwatch.Stop();

        var serialized = await agent.SerializeSessionAsync(session, cancellationToken: ct);
        return new ChatAgentRunResult(
            response.Text ?? string.Empty,
            serialized.GetRawText(),
            accumulator.TotalInputTokensProcessed,
            accumulator.TotalOutputTokensGenerated,
            accumulator.LatestPromptTokens,
            accumulator.LatestOutputTokens,
            accumulator.MaxPromptTokens,
            accumulator.MaxOutputTokens,
            accumulator.CallCount,
            stopwatch.ElapsedMilliseconds);
    }
}
