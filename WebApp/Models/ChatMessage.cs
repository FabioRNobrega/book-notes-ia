namespace WebApp.Models;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public long DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? InputTokensUsed { get; set; }
    public int? OutputTokensUsed { get; set; }
    public long? ResponseTimeMs { get; set; }
}
