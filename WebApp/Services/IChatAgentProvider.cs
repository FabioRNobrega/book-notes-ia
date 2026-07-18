using Microsoft.Agents.AI;

namespace WebApp.Services;

public interface IChatAgentProvider
{
    AIAgent GetAgent(string agentKey);
}
