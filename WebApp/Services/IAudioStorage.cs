namespace WebApp.Services;

public interface IAudioStorage
{
    Task WriteAsync(string key, byte[] data, CancellationToken ct = default);
    Task<byte[]?> ReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
