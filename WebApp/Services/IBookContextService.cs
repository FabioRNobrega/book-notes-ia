namespace WebApp.Services;

public interface IBookContextService
{
    Task<string?> GetContextAsync(Guid bookId, string userId);
    Task<string> GenerateAndSaveAsync(Guid bookId, string userId, CancellationToken ct = default);
    Task<string> SaveManualAsync(Guid bookId, string userId, string context);
    Task ClearAsync(Guid bookId, string userId);
}
