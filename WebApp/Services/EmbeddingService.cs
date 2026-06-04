using Microsoft.Extensions.AI;

namespace WebApp.Services;

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}

public sealed class EmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) : IEmbeddingService
{
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var embedding = await embeddingGenerator.GenerateAsync(text, cancellationToken: ct);
        return embedding.Vector.ToArray();
    }
}
