namespace WebApp.Models;

public class BookTitleEditViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public bool IsEditing { get; set; }
    public string? ErrorMessage { get; set; }
}
