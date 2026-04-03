using System.Text.Json;

namespace WebApp.Services;

public record UnsplashPhoto(string Url, string PhotographerName, string PhotographerProfileUrl);

public interface IUnsplashService
{
    Task<UnsplashPhoto?> GetBookPhotoAsync();
}

public class UnsplashService : IUnsplashService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICacheHandler _cache;
    private readonly ILogger<UnsplashService> _logger;
    private const string CacheKey = "unsplash:book-photo";

    private static readonly string[] Queries =
    [
        "old library books",
        "vintage books reading",
        "ancient library architecture",
        "books candlelight",
        "literary study room"
    ];

    public UnsplashService(IHttpClientFactory httpClientFactory, ICacheHandler cache, ILogger<UnsplashService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<UnsplashPhoto?> GetBookPhotoAsync()
    {
        var cached = await _cache.GetAsync(CacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                return JsonSerializer.Deserialize<UnsplashPhoto>(cached);
            }
            catch
            {
                // cached value malformed, fall through to fetch
            }
        }

        try
        {
            var query = Queries[Random.Shared.Next(Queries.Length)];
            var client = _httpClientFactory.CreateClient("Unsplash");
            var response = await client.GetAsync(
                $"/search/photos?query={Uri.EscapeDataString(query)}&orientation=landscape&content_filter=high&per_page=15"
            );
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");

            if (results.GetArrayLength() == 0)
                return null;

            var index = Random.Shared.Next(results.GetArrayLength());
            var photo = results[index];
            var url = photo.GetProperty("urls").GetProperty("regular").GetString();
            var name = photo.GetProperty("user").GetProperty("name").GetString() ?? "Unknown";
            var profileUrl = photo.GetProperty("user").GetProperty("links").GetProperty("html").GetString() ?? "#";

            if (string.IsNullOrEmpty(url))
                return null;

            var result = new UnsplashPhoto(url, name, profileUrl);

            await _cache.SetAsync(CacheKey, JsonSerializer.Serialize(result), TimeSpan.FromHours(6));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Unsplash photo");
            return null;
        }
    }
}
