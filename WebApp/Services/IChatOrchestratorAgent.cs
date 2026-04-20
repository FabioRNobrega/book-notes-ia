using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace WebApp.Services;

public sealed record ChatAgentRunResult(string ResponseText, string SerializedSessionJson);

public interface IChatOrchestratorAgent
{
    Task<ChatAgentRunResult> RunAsync(string message, string? sessionJson, string? instructions, CancellationToken ct = default);
}

public sealed class ChatOrchestratorAgent(AIAgent agent) : IChatOrchestratorAgent
{
    public async Task<ChatAgentRunResult> RunAsync(string message, string? sessionJson, string? instructions, CancellationToken ct = default)
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
                Instructions = instructions
            }
        };

        var response = await agent.RunAsync(message, session, runOptions, ct);
        var serialized = await agent.SerializeSessionAsync(session, cancellationToken: ct);
        return new ChatAgentRunResult(response.Text ?? string.Empty, serialized.GetRawText());
    }
}
