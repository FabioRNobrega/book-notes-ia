using System.ComponentModel.DataAnnotations;

namespace EbookParseService.Api.Models;

public sealed record ParseEpubRequest(
    [Required, StringLength(255)] string FileName);
