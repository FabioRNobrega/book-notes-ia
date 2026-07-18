namespace WebApp.Services;

public interface IChatCompletionService
{
    Task<string> CompleteAsync(string prompt, string agentKey, CancellationToken ct = default);
}
