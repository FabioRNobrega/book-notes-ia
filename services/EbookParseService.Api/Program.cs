using EbookParseService.Api.Options;
using EbookParseService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddOptions<EpubParserOptions>()
    .Bind(builder.Configuration.GetSection(EpubParserOptions.SectionName))
    .Validate(options => Path.IsPathFullyQualified(options.InputDirectory), "InputDirectory must be absolute.")
    .Validate(options => Path.IsPathFullyQualified(options.OutputDirectory), "OutputDirectory must be absolute.")
    .Validate(options => options.MaxSourceFileBytes > 0
        && options.MaxArchiveEntryCount > 0
        && options.MaxEntryBytes > 0
        && options.MaxTotalUncompressedBytes > 0
        && options.MaxXmlCharacters > 0, "All parser limits must be positive.")
    .ValidateOnStart();
builder.Services.AddSingleton<IEpubChapterParser, EpubChapterParser>();
builder.Services.AddSingleton<ITtsTextNormalizer, TtsTextNormalizer>();
builder.Services.AddSingleton<INumberToWordsConverter, NumberToWordsConverter>();
builder.Services.AddSingleton<IChapterOutputWriter, ChapterOutputWriter>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapControllers();

app.Run();

public partial class Program;
