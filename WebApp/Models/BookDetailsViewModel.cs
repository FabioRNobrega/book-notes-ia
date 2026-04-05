namespace WebApp.Models;

public class BookDetailsViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Author { get; set; } = default!;
    public string? CoverUrl { get; set; }
    public string? Context { get; set; }
    public IReadOnlyList<BookNoteViewModel> Notes { get; set; } = [];
}
