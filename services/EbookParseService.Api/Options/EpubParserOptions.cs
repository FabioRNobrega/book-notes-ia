namespace EbookParseService.Api.Options;

public sealed class EpubParserOptions
{
    public const string SectionName = "EpubParser";

    public string InputDirectory { get; set; } = "/data/input";
    public string OutputDirectory { get; set; } = "/data/output";
    public long MaxSourceFileBytes { get; set; } = 100 * 1024 * 1024;
    public int MaxArchiveEntryCount { get; set; } = 10_000;
    public long MaxEntryBytes { get; set; } = 20 * 1024 * 1024;
    public long MaxTotalUncompressedBytes { get; set; } = 500 * 1024 * 1024;
    public long MaxXmlCharacters { get; set; } = 25 * 1024 * 1024;
}
