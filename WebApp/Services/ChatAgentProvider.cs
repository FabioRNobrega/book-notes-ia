using Microsoft.Agents.AI;

namespace WebApp.Services;

public sealed class ChatAgentProvider(IServiceProvider services) : IChatAgentProvider
{
    public AIAgent GetAgent(string agentKey) =>
        services.GetRequiredKeyedService<AIAgent>(agentKey);
}
