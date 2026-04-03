using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Controllers;

[Authorize]
public class NotesController : Controller
{
    private const string PermittedExtension = ".txt";
    private readonly AppDbContext _db;
    private readonly IKindleClippingsImportService _importService;
    private readonly ILogger<NotesController> _logger;
    private readonly long _maxFileSizeBytes;

    public NotesController(AppDbContext db, IKindleClippingsImportService importService, ILogger<NotesController> logger, IConfiguration configuration)
    {
        _db = db;
        _importService = importService;
        _logger = logger;
        _maxFileSizeBytes = configuration.GetValue<long?>("NotesImport:MaxFileSizeBytes") ?? 1_048_576;
    }

    [HttpGet]
    public IActionResult Upload()
        => PartialView("~/Views/Home/_UploadNotes.cshtml", new UploadNotesViewModel());

    [HttpPost("/notes/import")]
    public async Task<IActionResult> Import([FromForm] IFormFile? file, CancellationToken ct)
    {
        var validationError = ValidateFile(file);
        if (validationError is not null)
        {
            return PartialView("~/Views/Home/_UploadNotes.cshtml", new UploadNotesViewModel
            {
                IsSuccess = false,
                StatusMessage = validationError
            });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            await using var stream = file!.OpenReadStream();
            var summary = await _importService.ImportAsync(userId, stream, ct);
            return PartialView("~/Views/Home/_UploadNotes.cshtml", new UploadNotesViewModel
            {
                IsSuccess = true,
                StatusMessage = BuildImportMessage(file.FileName, summary),
                Summary = summary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kindle import failed for user {UserId}", userId);
            return PartialView("~/Views/Home/_UploadNotes.cshtml", new UploadNotesViewModel
            {
                IsSuccess = false,
                StatusMessage = "We couldn't import this file right now. Please try again."
            });
        }
    }

    [HttpPost("/notes/import-chat")]
    public async Task<IActionResult> ImportFromChat([FromForm] IFormFile? file, CancellationToken ct)
    {
        var validationError = ValidateFile(file);
        if (validationError is not null)
        {
            return PartialView("~/Views/Chat/_BotMessage.cshtml", $"<p>{WebUtility.HtmlEncode(validationError)}</p>");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            await using var stream = file!.OpenReadStream();
            var summary = await _importService.ImportAsync(userId, stream, ct);
            var html = BuildChatImportMessage(file.FileName, summary);
            return PartialView("~/Views/Chat/_BotMessage.cshtml", html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat Kindle import failed for user {UserId}", userId);
            return PartialView("~/Views/Chat/_BotMessage.cshtml", "<p>We couldn't import this file right now. Please try again.</p>");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Library(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var books = await _db.Books
            .Where(x => x.UserId == userId)
            .Select(x => new BookCardViewModel
            {
                Id = x.Id,
                Title = x.Title,
                Author = x.Author,
                CoverUrl = x.CoverUrl,
                NotesCount = x.Notes.Count,
                UpdatedAt = x.UpdatedAt
            })
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);

        return PartialView("~/Views/Home/_SeeYourNotes.cshtml", new NotesLibraryViewModel
        {
            Books = books
        });
    }

    [HttpGet("/notes/book/{id:guid}")]
    public async Task<IActionResult> Book(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var book = await _db.Books
            .Where(x => x.Id == id && x.UserId == userId)
            .Select(x => new BookDetailsViewModel
            {
                Id = x.Id,
                Title = x.Title,
                Author = x.Author,
                CoverUrl = x.CoverUrl,
                Notes = x.Notes
                    .OrderByDescending(n => n.ClippedAtUtc)
                    .Select(n => new BookNoteViewModel
                    {
                        EntryType = n.EntryType,
                        LocationText = n.LocationText,
                        Content = n.Content,
                        ClippedAtUtc = n.ClippedAtUtc
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (book is null)
        {
            return PartialView("~/Views/Chat/_BotMessage.cshtml", "<p>We couldn't find that book in your library.</p>");
        }

        return PartialView("~/Views/Notes/_BookDetails.cshtml", book);
    }

    private string? ValidateFile(IFormFile? file)
    {
        if (file is null)
        {
            return "Please choose a Kindle clippings .txt file.";
        }

        if (file.Length <= 0)
        {
            return "The uploaded file is empty.";
        }

        if (file.Length > _maxFileSizeBytes)
        {
            return $"The uploaded file is too large. Maximum size is {_maxFileSizeBytes / 1024 / 1024} MB.";
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!string.Equals(extension, PermittedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return "Only Kindle clippings .txt files are supported in v1.";
        }

        return null;
    }

    private static string BuildImportMessage(string fileName, KindleImportSummary summary)
    {
        var safeFileName = Path.GetFileName(fileName);
        return $"Imported {safeFileName}: {summary.NotesImported} new notes, {summary.DuplicatesSkipped} duplicates skipped, {summary.InvalidEntriesSkipped} invalid entries skipped across {summary.BooksTouched} books.";
    }

    private static string BuildChatImportMessage(string fileName, KindleImportSummary summary)
    {
        var message = WebUtility.HtmlEncode(BuildImportMessage(fileName, summary));
        return $$"""
<p>{{message}}</p>
<div class="mt-3">
    <button type="button"
            hx-get="/Notes/Library"
            hx-target="#chat-container"
            hx-swap="innerHTML"
            onclick="window.BookNotesChat?.setMode('notes')"
            class="inline-flex items-center gap-2 border px-3 py-2 text-sm text-white/90 transition hover:bg-white/5"
            style="border-radius: var(--book-radius); border-color: rgba(255,255,255,0.2);">
        <sl-icon name="eye"></sl-icon>
        <span>See Your Notes</span>
    </button>
</div>
""";
    }
}
