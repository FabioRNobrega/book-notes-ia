using Microsoft.AspNetCore.Identity;
using Pgvector;

namespace WebApp.Models;

public class BookEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;
    public IdentityUser User { get; set; } = default!;

    public Guid BookId { get; set; }
    public Book Book { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string Author { get; set; } = default!;
    public Vector Embedding { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
