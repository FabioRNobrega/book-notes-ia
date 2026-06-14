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
{
    Console.WriteLine("[TTS] Placeholder mode enabled — returning 440 Hz tone instead of real synthesis.");
    builder.Services.AddSingleton<ITtsService, PlaceholderTtsService>();
}
else
{
    Console.WriteLine("[TTS] Real synthesis mode — loading Supertonic ONNX assets.");
    builder.Services.AddSingleton<ITtsService, SupertonicTtsService>();
}

var app = builder.Build();

app.MapControllers();

app.Run();
