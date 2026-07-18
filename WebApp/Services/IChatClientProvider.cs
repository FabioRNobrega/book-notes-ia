using Microsoft.Extensions.AI;

namespace WebApp.Services;

public interface IChatClientProvider
{
    IChatClient GetChatClient(string agentKey);
}
