using Microsoft.Extensions.AI;

namespace WebApp.Services;

public sealed class ChatClientProvider(IServiceProvider services) : IChatClientProvider
{
    public IChatClient GetChatClient(string agentKey) =>
        services.GetRequiredKeyedService<IChatClient>(agentKey);
}
