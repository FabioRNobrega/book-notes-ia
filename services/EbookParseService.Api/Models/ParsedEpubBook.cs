namespace EbookParseService.Api.Models;

public sealed record ParsedEpubBook(
    string SourceFileName,
    string Title,
    string Language,
    IReadOnlyList<ParsedChapter> Chapters);
