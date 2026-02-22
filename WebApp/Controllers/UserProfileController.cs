using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WebApp.Models;


namespace WebApp.Controllers
{
    [Authorize]
    public class UserProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserProfileController> _logger;
         private readonly RedisCacheService _cache;


        public UserProfileController(AppDbContext context, ILogger<UserProfileController> logger, RedisCacheService cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }


        // GET: UserProfile/MyProfile
        public async Task<IActionResult> MyProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

            // If missing, show an empty model with defaults
            profile ??= new UserProfile
            {
                PreferredLanguage = "en",
                AgentProfileCompact = "{}",
                AgentProfileVersion = 1
            };

            return View("Upsert", profile);
        }

        // GET: UserProfile/Upsert  (optional alias, nice for routing)
        public Task<IActionResult> Upsert() => MyProfile();

        // POST: UserProfile/Upsert
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(
            [Bind("Nickname,PreferredLanguage,TonePreference,LearningGoals,FavoriteAuthors,AboutMe")] UserProfile input,
            [FromForm(Name = "LearningStyle")] string? learningStyle,
            [FromForm(Name = "ReadingLanguages[]")] string[]? readingLanguages,
            [FromForm(Name = "LovedGenres[]")] string[]? lovedGenres,
            [FromForm(Name = "DislikedGenres[]")] string[]? dislikedGenres
        )
        {
            ModelState.Remove(nameof(UserProfile.UserId));
            ModelState.Remove(nameof(UserProfile.User));

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return PartialView("~/Views/Shared/Components/_Alert.cshtml",
                    (false, "You are not logged in. Please login again."));


            if (!ModelState.IsValid)
                return PartialView("~/Views/Shared/Components/_Alert.cshtml",
                    (false, "Please fix the form errors and try again."));

            var existing = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            var cacheKey = $"agentprofile:{userId}";
            var cacheTTL = TimeSpan.FromDays(365);

            if (existing is null)
            {
                var userProfile = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,

                    Nickname = input.Nickname,
                    PreferredLanguage = input.PreferredLanguage,
                    TonePreference = input.TonePreference,
                    LearningGoals = input.LearningGoals,
                    FavoriteAuthors = input.FavoriteAuthors,
                    AboutMe = input.AboutMe,

                    ReadingLanguages = ToJson(readingLanguages),
                    LearningStyle = ToJson(learningStyle is null ? null : new[] { learningStyle }),
                    LovedGenres = ToJson(lovedGenres),
                    DislikedGenres = ToJson(dislikedGenres),

                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    AgentProfileVersion = 1
                };
                userProfile.AgentProfileCompact = BuildAgentProfileCompactJson(1, userProfile);
                _context.Add(userProfile);

                await _context.SaveChangesAsync();
                await _cache.SetAsync(cacheKey, userProfile.AgentProfileCompact, cacheTTL);

                return PartialView("~/Views/Shared/Components/_Alert.cshtml",
                    (true, "Your profile has been created."));
            }

            // Update path
            existing.Nickname = input.Nickname;
            existing.PreferredLanguage = input.PreferredLanguage;
            existing.TonePreference = input.TonePreference;
            existing.LearningGoals = input.LearningGoals;
            existing.FavoriteAuthors = input.FavoriteAuthors;
            existing.AboutMe = input.AboutMe;

            existing.ReadingLanguages = ToJson(readingLanguages);
            existing.LearningStyle = ToJson(learningStyle is null ? null : new[] { learningStyle });
            existing.LovedGenres = ToJson(lovedGenres);
            existing.DislikedGenres = ToJson(dislikedGenres);

            existing.AgentProfileVersion = existing.AgentProfileVersion <= 0 ? 1 : existing.AgentProfileVersion + 1;
            existing.AgentProfileCompact = BuildAgentProfileCompactJson(existing.AgentProfileVersion, existing);

            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _cache.SetAsync(cacheKey, existing.AgentProfileCompact, cacheTTL);

            return PartialView("~/Views/Shared/Components/_Alert.cshtml",
                (true, "Your profile has been updated."));
        }



        private static System.Text.Json.JsonDocument? ToJson(string[]? values)
        {
            if (values is null || values.Length == 0)
                return null;

            var json = System.Text.Json.JsonSerializer.Serialize(values);
            return System.Text.Json.JsonDocument.Parse(json);
        }

        private static string BuildAgentProfileCompactJson(int profileVersion, UserProfile p)
        {
            var payload = new
            {
                profile_v = profileVersion,
                nickname = p.Nickname,
                preferred_language = p.PreferredLanguage,
                tone = p.TonePreference,
                learning_goals = p.LearningGoals,
                favorite_authors = p.FavoriteAuthors,
                about_me = p.AboutMe,
                reading_languages = JsonToArray(p.ReadingLanguages),
                learning_style = JsonToArray(p.LearningStyle),
                loved_genres = JsonToArray(p.LovedGenres),
                disliked_genres = JsonToArray(p.DislikedGenres),
            };

            var options = new JsonSerializerOptions
            {
                // keeps it compact; also prevents lots of null fields if you want
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };

            return JsonSerializer.Serialize(payload, options);
        }
        private static string[]? JsonToArray(JsonDocument? doc)
        {
            if (doc is null) return null;

            return doc.RootElement
                    .EnumerateArray()
                    .Select(e => e.GetString()!)
                    .Where(s => s is not null)
                    .ToArray();
        }
    }
}
