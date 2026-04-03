using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OllamaSharp;
using WebApp.Services;


var builder = WebApplication.CreateBuilder(args);

// Add Postgres connection
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
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
    var ollamaUrl = builder.Configuration["Ollama:OllamaURL"];
    var ollamaModel = builder.Configuration["Ollama:OllamaModel"];
    return new OllamaApiClient(new Uri(ollamaUrl), ollamaModel);
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

// Build Redis for cache handler
builder.Services.AddStackExchangeRedisCache(options =>
{  
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Register Unsplash HTTP client
builder.Services.AddHttpClient("Unsplash", client =>
{
    client.BaseAddress = new Uri("https://api.unsplash.com/");
    client.DefaultRequestHeaders.Add(
        "Authorization",
        $"Client-ID {builder.Configuration["Unsplash:AccessKey"]}"
    );
    client.DefaultRequestHeaders.Add("Accept-Version", "v1");
});
builder.Services.AddScoped<IUnsplashService, UnsplashService>();

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
    db.Database.Migrate();
}

app.Run();
