namespace WebApp.Models;

public class BookNote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookId { get; set; }
    public Book Book { get; set; } = default!;

    public string UserId { get; set; } = default!;
    public string EntryType { get; set; } = default!;
    public string LocationText { get; set; } = default!;
    public string Content { get; set; } = default!;
    public DateTime ClippedAtUtc { get; set; }
    public string DedupeKey { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
