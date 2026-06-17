using Microsoft.Extensions.Options;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class FileSystemAudioStorageTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly FileSystemAudioStorage _storage;

    public FileSystemAudioStorageTests()
    {
        Directory.CreateDirectory(_tempDir);
        _storage = new FileSystemAudioStorage(Options.Create(new AudioStorageOptions { BasePath = _tempDir }));
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task WriteAsync_ThenReadAsync_ReturnsSameBytes()
    {
        var data = new byte[] { 1, 2, 3, 4 };
        await _storage.WriteAsync("test.wav", data);
        var result = await _storage.ReadAsync("test.wav");
        Assert.Equal(data, result);
    }

    [Fact]
    public async Task ReadAsync_WhenKeyDoesNotExist_ReturnsNull()
    {
        var result = await _storage.ReadAsync("missing.wav");
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        await _storage.WriteAsync("delete-me.wav", new byte[] { 1 });
        await _storage.DeleteAsync("delete-me.wav");
        Assert.Null(await _storage.ReadAsync("delete-me.wav"));
    }

    [Fact]
    public async Task WriteAsync_CreatesIntermediateDirectories()
    {
        await _storage.WriteAsync("subdir/nested.wav", new byte[] { 99 });
        var result = await _storage.ReadAsync("subdir/nested.wav");
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("../escape.wav")]
    [InlineData("../../etc/passwd")]
    [InlineData("/absolute/path.wav")]
    public async Task WriteAsync_PathTraversalKey_Throws(string key)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _storage.WriteAsync(key, new byte[] { 1 }));
    }

    [Theory]
    [InlineData("../escape.wav")]
    [InlineData("../../etc/passwd")]
    public async Task ReadAsync_PathTraversalKey_Throws(string key)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _storage.ReadAsync(key));
    }

    [Fact]
    public async Task WriteAsync_ProducesStableStorageReference()
    {
        var key = $"audio/{Guid.NewGuid():N}_pt_F3.wav";
        var data = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        await _storage.WriteAsync(key, data);
        var read = await _storage.ReadAsync(key);
        Assert.Equal(data, read);
    }
}
