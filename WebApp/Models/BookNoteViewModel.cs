namespace WebApp.Models;

public class BookNoteViewModel
{
    public string EntryType { get; set; } = default!;
    public string LocationText { get; set; } = default!;
    public string Content { get; set; } = default!;
    public DateTime ClippedAtUtc { get; set; }
}
