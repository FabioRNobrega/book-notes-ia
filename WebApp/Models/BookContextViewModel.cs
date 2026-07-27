namespace WebApp.Models;

public class BookContextViewModel
{
    public Guid BookId { get; set; }
    public string Title { get; set; } = default!;
    public string Author { get; set; } = default!;
    public string? Context { get; set; }
}
