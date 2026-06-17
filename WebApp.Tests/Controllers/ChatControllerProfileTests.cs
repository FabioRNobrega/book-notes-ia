using System.Reflection;
using WebApp.Controllers;

namespace WebApp.Tests.Controllers;

public class ChatControllerProfileTests
{
    [Fact]
    public void BuildProfileInstructions_WithAllFieldsSet_ReturnsSingleLine()
    {
        var result = InvokeBuildProfileInstructions(
            """
            {
              "nickname": "Fabio",
              "preferred_language": "Portuguese",
              "tone": "concise",
              "learning_goals": "understand philosophy",
              "favorite_authors": "Ursula K. Le Guin",
              "about_me": "curious reader",
              "reading_languages": ["Portuguese", "English"],
              "learning_style": ["examples"],
              "loved_genres": ["science fiction"],
              "disliked_genres": ["grimdark"]
            }
            """);

        Assert.NotNull(result);
        Assert.DoesNotContain('\n', result);
        Assert.DoesNotContain("not set", result);
        Assert.Contains("Fabio", result);
        Assert.Contains("reads in Portuguese", result);
        Assert.Contains("likes science fiction", result);
    }

    [Fact]
    public void BuildProfileInstructions_WithEmptyFields_OmitsThemFromSentence()
    {
        var result = InvokeBuildProfileInstructions(
            """
            {
              "nickname": "Fabio",
              "preferred_language": "",
              "loved_genres": []
            }
            """);

        Assert.Equal("Reader: Fabio.", result);
    }

    [Fact]
    public void BuildProfileInstructions_WithNoFields_ReturnsNull()
    {
        var result = InvokeBuildProfileInstructions("{}");

        Assert.Null(result);
    }

    [Fact]
    public void BuildOrchestratorInstructions_DoesNotContainBookTitles()
    {
        var result = InvokeBuildOrchestratorInstructions(null);

        Assert.DoesNotContain("User's book library", result);
        Assert.DoesNotContain("Foundation", result);
        Assert.Contains("any specific book or title", result);
    }

    [Fact]
    public void BuildOrchestratorInstructions_RequiresPreferredLanguageInResponse()
    {
        var result = InvokeBuildOrchestratorInstructions(null, "English");

        Assert.Contains("English", result);
        Assert.Contains("mandatory", result);
        Assert.Contains("translate", result, StringComparison.OrdinalIgnoreCase);
    }

    private static string? InvokeBuildProfileInstructions(string json)
    {
        var method = typeof(ChatController).GetMethod("BuildProfileInstructions", BindingFlags.Static | BindingFlags.NonPublic);
        return (string?)method!.Invoke(null, [json]);
    }

    private static string InvokeBuildOrchestratorInstructions(string? profileInstructions, string? preferredLanguage = null)
    {
        var method = typeof(ChatController).GetMethod("BuildOrchestratorInstructions", BindingFlags.Static | BindingFlags.NonPublic);
        return Assert.IsType<string>(method!.Invoke(null, [profileInstructions, preferredLanguage]));
    }
}
