using Microsoft.AspNetCore.Identity;

namespace WebApp.Models;

public class Book
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;
    public IdentityUser User { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string Author { get; set; } = default!;
    public string NormalizedTitle { get; set; } = default!;
    public string NormalizedAuthor { get; set; } = default!;
    public string? CoverUrl { get; set; }
    public string? Context { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BookNote> Notes { get; set; } = [];
}
