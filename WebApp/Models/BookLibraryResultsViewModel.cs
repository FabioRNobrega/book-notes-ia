namespace WebApp.Models;

public class BookLibraryResultsViewModel
{
    public IReadOnlyList<BookCardViewModel> Books { get; set; } = [];
    public string? HeaderMessage { get; set; }
    public bool IsLibrarianNotFound { get; set; }
}
