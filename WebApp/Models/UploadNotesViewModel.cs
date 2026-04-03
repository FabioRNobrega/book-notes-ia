using WebApp.Services;

namespace WebApp.Models;

public class UploadNotesViewModel
{
    public string? StatusMessage { get; set; }
    public bool IsSuccess { get; set; }
    public KindleImportSummary? Summary { get; set; }
}
