namespace EbookParseService.Api.Models;

public sealed record ParsedChapter(int Number, IReadOnlyList<string> Paragraphs);
