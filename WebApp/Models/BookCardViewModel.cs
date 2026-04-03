namespace WebApp.Models;

public class BookCardViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Author { get; set; } = default!;
    public string? CoverUrl { get; set; }
    public int NotesCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
