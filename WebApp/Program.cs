using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Npgsql;
using OllamaSharp;
using Pgvector.EntityFrameworkCore;
using Pgvector.Npgsql;
using WebApp.Services;


var builder = WebApplication.CreateBuilder(args);

var postgresConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is not configured.");

builder.Services.AddSingleton(_ =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(postgresConnectionString);
    dataSourceBuilder.UseVector();
    return dataSourceBuilder.Build();
});

// Add Postgres connection
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(
        sp.GetRequiredService<NpgsqlDataSource>(),
        npgsql => npgsql.UseVector()
    )
);

// Add Identity builder
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>();


// Build with Ollama
builder.Services.AddSingleton<IChatClient>(_ =>
{
    var ollamaUrl = builder.Configuration["Ollama:OllamaURL"] ?? "http://ollama:11434";
    var ollamaModel = builder.Configuration["Ollama:OllamaModel"] ?? "qwen3.5:4b";
    var ollamaTimeoutSeconds = builder.Configuration.GetValue<int?>("Ollama:TimeoutSeconds") ?? 300;
    var httpClient = new HttpClient
    {
        BaseAddress = new Uri(ollamaUrl),
        Timeout = TimeSpan.FromSeconds(ollamaTimeoutSeconds)
    };

    var numCtx = builder.Configuration.GetValue<int?>("Ollama:NumCtx") ?? 8192;

    return ((IChatClient)new TokenCountingChatClient(new OllamaApiClient(httpClient, ollamaModel)))
        .AsBuilder()
        .ConfigureOptions(options =>
        {
            options.Temperature = 0; // sets the temperature 0 to 1 [higher is more creative, lower is more coherent]
            options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            options.AdditionalProperties["think"] = false; // This disable think mode on qwen to faster responses
            options.AdditionalProperties["num_ctx"] = numCtx;
        })
        .Build();
});

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
{
    var ollamaUrl = builder.Configuration["Ollama:OllamaURL"] ?? "http://ollama:11434";
    var ollamaTimeoutSeconds = builder.Configuration.GetValue<int?>("Ollama:TimeoutSeconds") ?? 300;
    var httpClient = new HttpClient
    {
        BaseAddress = new Uri(ollamaUrl),
        Timeout = TimeSpan.FromSeconds(ollamaTimeoutSeconds)
    };

    return (IEmbeddingGenerator<string, Embedding<float>>)new OllamaApiClient(httpClient, "mxbai-embed-large");
});

builder.Services.AddSingleton<AIAgent>(sp =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();

    return new ChatClientAgent(
        chatClient,
        name: "LocalOllamaAgent",
        instructions: 
            """
            You are a helpful assistant.
            Be concise and practical.
            When giving recommendations, explain briefly why they match the user's preferences.
            """
    );
});
builder.Services.AddSingleton<IChatOrchestratorAgent, ChatOrchestratorAgent>();
builder.Services.AddScoped<IBookContextAgentTool, BookContextAgentTool>();
builder.Services.AddScoped<IBookNotesAnalysisService, BookNotesAnalysisService>();
builder.Services.AddScoped<IBookNotesAgentTool, BookNotesAgentTool>();
builder.Services.AddScoped<IBookNoteSearchService, BookNoteSearchService>();
builder.Services.AddScoped<IBookNoteSearchAgentTool, BookNoteSearchAgentTool>();

// Build Redis for cache handler
builder.Services.AddStackExchangeRedisCache(options =>
{  
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Register Unsplash HTTP client
builder.Services.AddHttpClient("Unsplash", client =>
{
    client.BaseAddress = new Uri("https://api.unsplash.com/");

    var unsplashAccessKey = builder.Configuration["Unsplash:AccessKey"];
    if (!string.IsNullOrWhiteSpace(unsplashAccessKey))
    {
        client.DefaultRequestHeaders.Add(
            "Authorization",
            $"Client-ID {unsplashAccessKey}"
        );
    }

    client.DefaultRequestHeaders.Add("Accept-Version", "v1");
});
builder.Services.AddScoped<IUnsplashService, UnsplashService>();
builder.Services.AddScoped<IKindleClippingsImportService, KindleClippingsImportService>();
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<IBookContextService, BookContextService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IBookLookupService, BookLookupService>();
builder.Services.AddScoped<IBookLibrarySearchService, BookLibrarySearchService>();
builder.Services.AddScoped<ILibrarianBookSearchService, LibrarianBookSearchService>();

var notesImportFileSizeLimit = builder.Configuration.GetValue<long?>("NotesImport:MaxFileSizeBytes") ?? 1_048_576;
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = notesImportFileSizeLimit;
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ICacheHandler, CacheHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Identity UI (Razor Pages)
app.MapRazorPages();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
    db.Database.Migrate();
    dataSource.ReloadTypes();
}

app.Run();
