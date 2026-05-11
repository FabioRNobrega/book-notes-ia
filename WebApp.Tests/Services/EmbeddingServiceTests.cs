using Microsoft.Extensions.AI;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class EmbeddingServiceTests
{
    [Fact]
    public async Task EmbedAsync_CallsGeneratorOnce_ReturnsVector()
    {
        var expected = Enumerable.Range(0, 1024).Select(i => (float)i).ToArray();
        var generator = new FakeEmbeddingGenerator(expected);
        var service = new EmbeddingService(generator);

        var result = await service.EmbedAsync("Dune by Frank Herbert", CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal(1, generator.CallCount);
        Assert.Equal("Dune by Frank Herbert", generator.LastValue);
    }

    private sealed class FakeEmbeddingGenerator(float[] vector) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public int CallCount { get; private set; }
        public string? LastValue { get; private set; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastValue = values.Single();
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(
                [new Embedding<float>(vector)]));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
