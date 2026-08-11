namespace EbookParseService.Api.Models;

public sealed record ParseEpubResponse(
    string SourceFileName,
    string Title,
    string Language,
    string OutputDirectory,
    int ChapterCount,
    IReadOnlyList<ParsedChapterSummary> Chapters);

public sealed record ParsedChapterSummary(int Number, string FileName, int CharacterCount);
