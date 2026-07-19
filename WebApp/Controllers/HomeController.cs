using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Controllers;
[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IUnsplashService _unsplash;
    private readonly ICacheHandler _cache;

    public HomeController(ILogger<HomeController> logger, IUnsplashService unsplash, ICacheHandler cache)
    {
        _logger = logger;
        _unsplash = unsplash;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var photo = await _unsplash.GetBookPhotoAsync();
        ViewData["BackgroundPhoto"] = photo;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewData["ContextUsagePct"] = 0;
        ViewData["ActiveAgent"] = ChatAgentCatalog.DefaultKey;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            ViewData["ActiveAgent"] = ChatController.NormalizeAgentKey(await _cache.GetAsync($"activeagent:{userId}"));

            var sessionIdRaw = await _cache.GetAsync($"activesessionid:{userId}");
            if (Guid.TryParse(sessionIdRaw, out var sessionId))
            {
                var contextJson = await _cache.GetAsync($"agentcontext:{userId}:{sessionId:D}");
                if (!string.IsNullOrWhiteSpace(contextJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(contextJson);
                        if (doc.RootElement.TryGetProperty("contextUsagePct", out var pctEl) &&
                            pctEl.TryGetInt32(out var pct))
                        {
                            ViewData["ContextUsagePct"] = Math.Clamp(pct, 0, 100);
                        }
                    }
                    catch (JsonException) { }
                }
            }
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult UploadNotes()
    {
        return PartialView("_UploadNotes");
    }

    public IActionResult SeeYourNotes()
    {
        return PartialView("_SeeYourNotes");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
