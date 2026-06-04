using Pgvector;

namespace WebApp.Models;

public class BookNoteEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;

    public Guid BookId { get; set; }
    public Book Book { get; set; } = default!;

    public Guid BookNoteId { get; set; }
    public BookNote BookNote { get; set; } = default!;

    public Vector Embedding { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
