using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace WebApp.Services;

public interface ICacheHandler
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default);
    Task SetObjectAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
public sealed class CacheHandler(IDistributedCache cache) : ICacheHandler
{
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
        => cache.GetStringAsync(key, ct);

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => cache.RemoveAsync(key, ct);

    // For already-serialized JSON
    public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        return cache.SetStringAsync(key, value, options, ct);
    }

    // For normal objects
    public Task SetObjectAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value, JsonOpts);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        return cache.SetStringAsync(key, json, options, ct);
    }
}