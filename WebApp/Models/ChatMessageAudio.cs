namespace WebApp.Models;

public class ChatMessageAudio
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChatMessageId { get; set; }
    public ChatMessage ChatMessage { get; set; } = default!;

    public string Language { get; set; } = default!;
    public string Voice { get; set; } = default!;

    public string StorageKey { get; set; } = default!;
    public string ContentType { get; set; } = "audio/wav";
    public long ByteLength { get; set; }

    public double? DurationSeconds { get; set; }
    public string? ContentHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
