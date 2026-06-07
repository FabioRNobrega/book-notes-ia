using System.Text.Json;

namespace WebApp.Services;

public interface IOpenLibraryService
{
    Task<string?> GetSynopsisAsync(string title, string author, CancellationToken ct = default);
}

public sealed class OpenLibraryService(
    HttpClient httpClient,
    ILogger<OpenLibraryService> logger) : IOpenLibraryService
{
    public async Task<string?> GetSynopsisAsync(string title, string author, CancellationToken ct = default)
    {
        try
        {
            var workKey = await GetWorkKeyAsync(title, author, ct);
            if (string.IsNullOrWhiteSpace(workKey))
                return null;

            return await GetWorkDescriptionAsync(workKey, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to fetch Open Library synopsis for {Title} by {Author}.", title, author);
            return null;
        }
    }

    private async Task<string?> GetWorkKeyAsync(string title, string author, CancellationToken ct)
    {
        var url = $"https://openlibrary.org/search.json?title={Uri.EscapeDataString(title)}&author={Uri.EscapeDataString(author)}";
        logger.LogInformation("Requesting Open Library search for {Title} by {Author}.", title, author);

        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!document.RootElement.TryGetProperty("docs", out var docs) ||
            docs.ValueKind != JsonValueKind.Array ||
            docs.GetArrayLength() == 0)
        {
            logger.LogInformation("Open Library search returned no docs for {Title} by {Author}.", title, author);
            return null;
        }

        var firstDoc = docs[0];
        if (!firstDoc.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String)
            return null;

        var workKey = key.GetString();
        if (string.IsNullOrWhiteSpace(workKey))
        {
            logger.LogInformation("Open Library search result did not include a work key for {Title} by {Author}.", title, author);
            return null;
        }

        logger.LogInformation("Open Library search matched work key {WorkKey} for {Title} by {Author}.", workKey, title, author);
        return workKey;
    }

    private async Task<string?> GetWorkDescriptionAsync(string workKey, CancellationToken ct)
    {
        logger.LogInformation("Requesting Open Library work details for {WorkKey}.", workKey);

        using var response = await httpClient.GetAsync($"https://openlibrary.org{workKey}.json", ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!document.RootElement.TryGetProperty("description", out var description))
        {
            logger.LogInformation("Open Library work {WorkKey} did not include a description.", workKey);
            return null;
        }

        var synopsis = description.ValueKind switch
        {
            JsonValueKind.String => description.GetString(),
            JsonValueKind.Object when description.TryGetProperty("value", out var value) &&
                                      value.ValueKind == JsonValueKind.String => value.GetString(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(synopsis))
        {
            logger.LogInformation("Open Library work {WorkKey} included an empty description.", workKey);
            return null;
        }

        logger.LogInformation("Open Library work {WorkKey} returned a synopsis.", workKey);
        return synopsis;
    }
}
