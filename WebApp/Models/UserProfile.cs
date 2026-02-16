using System.Text.Json;
using Microsoft.AspNetCore.Identity;

namespace WebApp.Models;

public class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;
    public IdentityUser User { get; set; } = default!;

    public string Nickname { get; set; } = default!;
    public string PreferredLanguage { get; set; } = "en";

    // JSONB in Postgres
    public JsonDocument? ReadingLanguages { get; set; }
    public JsonDocument? LearningStyle { get; set; }
    public JsonDocument? LovedGenres { get; set; }
    public JsonDocument? DislikedGenres { get; set; }

    public string? TonePreference { get; set; }
    public string? LearningGoals { get; set; }
    public string? FavoriteAuthors { get; set; }
    public string? AboutMe { get; set; }

    public string AgentProfileCompact { get; set; } = "";
    public int AgentProfileVersion { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
