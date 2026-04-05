namespace WebApp.Services;

public interface IOllamaService
{
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
}
