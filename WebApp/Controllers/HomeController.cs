using System.Diagnostics;
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

    public HomeController(ILogger<HomeController> logger, IUnsplashService unsplash)
    {
        _logger = logger;
        _unsplash = unsplash;
    }

    public async Task<IActionResult> Index()
    {
        var photo = await _unsplash.GetBookPhotoAsync();
        ViewData["BackgroundPhoto"] = photo;
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
