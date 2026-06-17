using Microsoft.Extensions.Options;

namespace WebApp.Services;

public sealed class AudioStorageOptions
{
    public string BasePath { get; init; } = "/audio-storage";
}

public sealed class FileSystemAudioStorage(IOptions<AudioStorageOptions> options) : IAudioStorage
{
    private readonly string _basePath = options.Value.BasePath;

    public async Task WriteAsync(string key, byte[] data, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, data, ct);
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        if (!File.Exists(fullPath))
            return null;
        return await File.ReadAllBytesAsync(fullPath, ct);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        // Guard against path traversal: key must not escape the base path
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, key));
        if (!fullPath.StartsWith(Path.GetFullPath(_basePath), StringComparison.Ordinal))
            throw new InvalidOperationException($"Storage key '{key}' attempts path traversal.");
        return fullPath;
    }
}
