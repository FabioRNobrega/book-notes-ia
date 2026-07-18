namespace WebApp.Models;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? AgentType { get; set; }
    public string Content { get; set; } = string.Empty;
    public long DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? TotalInputTokensProcessed { get; set; }
    public int? TotalOutputTokensGenerated { get; set; }
    public int? LatestPromptTokens { get; set; }
    public int? MaxPromptTokens { get; set; }
    public int? ContextUsagePct { get; set; }
    public int? ModelCallCount { get; set; }
    public long? ResponseTimeMs { get; set; }
}
