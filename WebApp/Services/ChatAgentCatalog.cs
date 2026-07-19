namespace WebApp.Services;

public enum ChatAgentCategory
{
    Premium,
    FreeLocal
}

public sealed record ChatAgentCatalogEntry(
    string Key,
    string MenuTitle,
    string Subtitle,
    string ShortLabel,
    ChatAgentCategory Category,
    string? OllamaModel,
    bool IsDefault = false);

public static class ChatAgentCatalog
{
    public const string PremiumKey = "premium";
    public const string DefaultKey = "free-qwen";

    public static readonly IReadOnlyList<ChatAgentCatalogEntry> Entries =
    [
        new(PremiumKey, "Premium — ChatGPT", "Azure OpenAI · cloud", "Premium", ChatAgentCategory.Premium, null),
        new(DefaultKey, "Free — Qwen 3.5", "Local · private · free", "Free · Qwen 3.5", ChatAgentCategory.FreeLocal, "qwen3.5:4b", IsDefault: true),
        new("free-llama3", "Free — Llama 3.2", "Local · private · free", "Free · Llama 3.2", ChatAgentCategory.FreeLocal, "llama3.2:3b"),
        new("free-phi4", "Free — Phi-4 Mini", "Local · private · free", "Free · Phi-4 Mini", ChatAgentCategory.FreeLocal, "phi4-mini:3.8b"),
        new("free-granite4", "Free — Granite 4", "Local · private · free", "Free · Granite 4", ChatAgentCategory.FreeLocal, "granite4:3b"),
    ];

    public static readonly IReadOnlyList<ChatAgentCatalogEntry> FreeLocalEntries =
        Entries.Where(e => e.Category == ChatAgentCategory.FreeLocal).ToList();

    private static readonly IReadOnlyDictionary<string, ChatAgentCatalogEntry> ByKey =
        Entries.ToDictionary(e => e.Key, e => e, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? key)
    {
        if (string.Equals(key, "free", StringComparison.OrdinalIgnoreCase))
            return DefaultKey;

        return key is not null && ByKey.TryGetValue(key, out var entry) ? entry.Key : DefaultKey;
    }

    public static ChatAgentCatalogEntry Get(string? key) => ByKey[Normalize(key)];

    public static string? GetLabel(string? agentType) =>
        string.IsNullOrWhiteSpace(agentType) ? null : Get(agentType).ShortLabel;
}
