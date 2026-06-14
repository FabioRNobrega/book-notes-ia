using TtsService.Api.Options;
using TtsService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<SupertonicOptions>(
    builder.Configuration.GetSection("Supertonic"));

builder.Services.Configure<TtsDefaultsOptions>(
    builder.Configuration.GetSection("TtsDefaults"));

builder.Services.AddSingleton<VoiceResolver>();

var usePlaceholder = builder.Configuration.GetValue<bool>("Tts:UsePlaceholder");
if (usePlaceholder)
    builder.Services.AddSingleton<ITtsService, PlaceholderTtsService>();
else
    builder.Services.AddSingleton<ITtsService, SupertonicTtsService>();

var app = builder.Build();

app.MapControllers();

app.Run();
