using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Models;

namespace WebApp.Controllers
{
    [Authorize]
    public class UserProfileController : Controller
    {
        private readonly AppDbContext _context;

        public UserProfileController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Single entry point for the logged-in user profile.
        /// If the profile exists, go to Edit; otherwise go to Create.
        /// </summary>
        public async Task<IActionResult> MyProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile is null)
                return RedirectToAction(nameof(Create));

            return View("Edit", profile);
        }

        // GET: UserProfile/Create
        public async Task<IActionResult> Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            // If profile already exists, don't allow creating another one.
            var exists = await _context.UserProfiles.AnyAsync(p => p.UserId == userId);
            if (exists)
                return RedirectToAction(nameof(MyProfile));

            // Pre-fill minimal defaults (adjust as you like)
            var model = new UserProfile
            {
                PreferredLanguage = "en",
                AgentProfileCompact = "",
                AgentProfileVersion = 1
            };

            return View(model);
        }

        // POST: UserProfile/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Nickname,PreferredLanguage,TonePreference,LearningGoals,FavoriteAuthors,AboutMe")] UserProfile userProfile,
            [FromForm] string[]? readingLanguages,
            [FromForm] string? learningStyle,
            [FromForm] string[]? lovedGenres,
            [FromForm] string[]? dislikedGenres)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var exists = await _context.UserProfiles.AnyAsync(p => p.UserId == userId);
            if (exists)
                return RedirectToAction(nameof(MyProfile));

            if (!ModelState.IsValid)
                return View(userProfile);

            userProfile.Id = Guid.NewGuid();
            userProfile.UserId = userId;

            userProfile.ReadingLanguages = ToJson(readingLanguages);
            userProfile.LearningStyle = ToJson(learningStyle is null ? null : new[] { learningStyle });
            userProfile.LovedGenres = ToJson(lovedGenres);
            userProfile.DislikedGenres = ToJson(dislikedGenres);

            userProfile.AgentProfileCompact = "";
            userProfile.AgentProfileVersion = 1;
            userProfile.CreatedAt = DateTime.UtcNow;
            userProfile.UpdatedAt = DateTime.UtcNow;

            _context.Add(userProfile);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyProfile));
        }


        // GET: UserProfile/Edit/<id>
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id is null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.Id == id);
            if (userProfile is null)
                return NotFound();

            // Prevent editing someone else's profile
            if (!string.Equals(userProfile.UserId, userId, StringComparison.Ordinal))
                return Forbid();

            return View(userProfile);
        }

        // POST: UserProfile/Edit/<id>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind(
            "Id,Nickname,PreferredLanguage,ReadingLanguages,LearningStyle,LovedGenres,DislikedGenres,TonePreference,LearningGoals,FavoriteAuthors,AboutMe"
        )] UserProfile input)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            if (id != input.Id)
                return NotFound();

            // Load the real row from DB so we don't overpost system fields.
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.Id == id);
            if (userProfile is null)
                return NotFound();

            if (!string.Equals(userProfile.UserId, userId, StringComparison.Ordinal))
                return Forbid();

            if (!ModelState.IsValid)
                return View(input);

            // Copy editable fields
            userProfile.Nickname = input.Nickname;
            userProfile.PreferredLanguage = input.PreferredLanguage;
            userProfile.ReadingLanguages = input.ReadingLanguages;
            userProfile.LearningStyle = input.LearningStyle;
            userProfile.LovedGenres = input.LovedGenres;
            userProfile.DislikedGenres = input.DislikedGenres;
            userProfile.TonePreference = input.TonePreference;
            userProfile.LearningGoals = input.LearningGoals;
            userProfile.FavoriteAuthors = input.FavoriteAuthors;
            userProfile.AboutMe = input.AboutMe;

            // System-controlled fields
            userProfile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(MyProfile));
        }

        // Optional: keep Index/Details/Delete out of the UI for now.
        // If you want, you can remove these actions entirely.
        // For safety, the ones below only show the current user's profile.

        // GET: UserProfile
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var list = await _context.UserProfiles.Where(p => p.UserId == userId).ToListAsync();
            return View(list);
        }

        // GET: UserProfile/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id is null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(m => m.Id == id);
            if (userProfile is null)
                return NotFound();

            if (!string.Equals(userProfile.UserId, userId, StringComparison.Ordinal))
                return Forbid();

            return View(userProfile);
        }

        // GET: UserProfile/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id is null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(m => m.Id == id);
            if (userProfile is null)
                return NotFound();

            if (!string.Equals(userProfile.UserId, userId, StringComparison.Ordinal))
                return Forbid();

            return View(userProfile);
        }

        // POST: UserProfile/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.Id == id);
            if (userProfile is null)
                return RedirectToAction(nameof(MyProfile));

            if (!string.Equals(userProfile.UserId, userId, StringComparison.Ordinal))
                return Forbid();

            _context.UserProfiles.Remove(userProfile);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyProfile));
        }

        private static System.Text.Json.JsonDocument? ToJson(string[]? values)
        {
            if (values is null || values.Length == 0)
                return null;

            var json = System.Text.Json.JsonSerializer.Serialize(values);
            return System.Text.Json.JsonDocument.Parse(json);
        }

    }
}
