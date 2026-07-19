using WebApp.Services;

namespace WebApp.Tests.Services;

public class ChatAgentCatalogTests
{
    [Fact]
    public void Entries_ContainsPremiumAndFourFreeLocalModels()
    {
        var keys = ChatAgentCatalog.Entries.Select(e => e.Key).ToArray();

        Assert.Equal(["premium", "free-qwen", "free-llama3", "free-phi4", "free-granite4"], keys);
    }

    [Fact]
    public void FreeLocalEntries_MapToExpectedOllamaModelNames()
    {
        var models = ChatAgentCatalog.FreeLocalEntries.ToDictionary(e => e.Key, e => e.OllamaModel);

        Assert.Equal("qwen3.5:4b", models["free-qwen"]);
        Assert.Equal("llama3.2:3b", models["free-llama3"]);
        Assert.Equal("phi4-mini:3.8b", models["free-phi4"]);
        Assert.Equal("granite4:3b", models["free-granite4"]);
    }

    [Fact]
    public void FreeLocalEntries_DoesNotIncludePremium()
    {
        Assert.DoesNotContain(ChatAgentCatalog.FreeLocalEntries, e => e.Key == "premium");
    }

    [Theory]
    [InlineData("premium", "premium")]
    [InlineData("free-qwen", "free-qwen")]
    [InlineData("free-llama3", "free-llama3")]
    [InlineData("free-phi4", "free-phi4")]
    [InlineData("free-granite4", "free-granite4")]
    public void Normalize_WithSupportedKey_ReturnsSameKey(string input, string expected)
    {
        Assert.Equal(expected, ChatAgentCatalog.Normalize(input));
    }

    [Fact]
    public void Normalize_WithLegacyFreeKey_ReturnsDefaultFreeKey()
    {
        Assert.Equal("free-qwen", ChatAgentCatalog.Normalize("free"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-model")]
    public void Normalize_WithInvalidOrEmptyKey_ReturnsDefaultFreeKey(string? input)
    {
        Assert.Equal("free-qwen", ChatAgentCatalog.Normalize(input));
    }

    [Fact]
    public void Get_ReturnsDefaultFreeEntry_ForDefaultKey()
    {
        var entry = ChatAgentCatalog.Get("free-qwen");

        Assert.True(entry.IsDefault);
        Assert.Equal(ChatAgentCategory.FreeLocal, entry.Category);
    }

    [Theory]
    [InlineData("premium", "Premium")]
    [InlineData("free-qwen", "Free · Qwen 3.5")]
    [InlineData("free-llama3", "Free · Llama 3.2")]
    [InlineData("free-phi4", "Free · Phi-4 Mini")]
    [InlineData("free-granite4", "Free · Granite 4")]
    [InlineData("free", "Free · Qwen 3.5")]
    public void GetLabel_ReturnsFriendlyLabelForEachKey(string agentType, string expectedLabel)
    {
        Assert.Equal(expectedLabel, ChatAgentCatalog.GetLabel(agentType));
    }

    [Fact]
    public void GetLabel_WithNullOrEmpty_ReturnsNull()
    {
        Assert.Null(ChatAgentCatalog.GetLabel(null));
        Assert.Null(ChatAgentCatalog.GetLabel(""));
    }

    [Fact]
    public void MenuTitlesAndSubtitles_DoNotMentionOllama()
    {
        foreach (var entry in ChatAgentCatalog.Entries)
        {
            Assert.DoesNotContain("Ollama", entry.MenuTitle, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Ollama", entry.Subtitle, StringComparison.OrdinalIgnoreCase);
        }
    }
}
