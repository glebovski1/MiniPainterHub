using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Data;

public sealed class DevelopmentContentSeeder
{
    private static readonly IReadOnlyList<DevelopmentSeedUser> SeedUsers =
    [
        new(
            "admin",
            "admin@local",
            "P@ssw0rd!",
            "Elena Hart",
            new[] { "Admin", "User" },
            "Display painter and studio founder documenting grimdark armies and bright skirmish bands.",
            [
                new("Studio kickoff: 2026 painting goals", "Resetting the hobby desk this week with a focus on cleaner blends, faster basing, and finally finishing the swamp warband."),
                new("WIP: rusted sentinel captain", "Tried a warmer copper undercoat before the turquoise weathering pass. The contrast is much stronger than my last attempt.")
            ]),
        new(
            "user",
            "user@local",
            "User123!",
            "Marcus Vale",
            new[] { "User" },
            "Weekend hobbyist painting historical infantry, terrain scatter, and anything with a weathered coat.",
            [
                new("First squad done this month", "Batch painting is finally clicking. I limited the palette to three greens and it kept the whole unit cohesive."),
                new("Snow basing experiment", "Used crushed glass, matte medium, and a tiny bit of blue wash. It reads colder in person than in the reference photos.")
            ]),
        new(
            "studiomod",
            "studiomod@local",
            "StudioMod123!",
            "Priya Nair",
            new[] { "Moderator", "User" },
            "Community moderator focused on clean tutorials, readable color recipes, and approachable feedback.",
            [
                new("Quick tip: smoother cloaks", "Thin the midtone one step more than you think you need, then glaze shadows back in instead of trying to nail the blend in one pass."),
                new("Palette challenge thread idea", "Thinking about a weekly prompt built around one accent color and one texture goal. Could be a good way to keep posts focused.")
            ]),
        new(
            "inkandiron",
            "inkandiron@example.test",
            "Palette123!",
            "Jonah Mercer",
            new[] { "User" },
            "Paints battered sci-fi crews with lots of oil streaking, sponge chipping, and fluorescent lenses.",
            [
                new("Hazard stripes on boarding shields", "Masked the stripes instead of freehanding them. Slower setup, but the end result is crisp enough for close-up shots."),
                new("Neon lens recipe", "White ink dot, magenta glaze, then a final hot pink edge. It pops even under flat desk lighting.")
            ]),
        new(
            "velvetwash",
            "velvetwash@example.test",
            "Palette123!",
            "Sofia Klein",
            new[] { "User" },
            "Loves rich velvets, jewel tones, and display-scale fantasy heroes with dramatic capes.",
            [
                new("Burgundy cloak progression", "Started from a deep brown-red and pushed highlights with muted rose instead of pure red. The fabric looks heavier now."),
                new("Display plinth thoughts", "Simple black plinth, matte finish, no nameplate. The miniature needed breathing room more than extra decoration.")
            ]),
        new(
            "brasslantern",
            "brasslantern@example.test",
            "Palette123!",
            "Theo Barrett",
            new[] { "User" },
            "Terrain builder mixing scratch-built ruins, resin debris, and muddy pigments for campaign boards.",
            [
                new("Ruined archway ready for primer", "I added one more broken beam to make the silhouette less symmetrical. It feels more believable now."),
                new("Mud mix that survived varnish", "Pigment plus acrylic paste plus a drop of gloss medium. It kept the wet look after sealing.")
            ]),
        new(
            "ashenreed",
            "ashenreed@example.test",
            "Palette123!",
            "Nadia Brooks",
            new[] { "User" },
            "Focuses on desaturated fantasy palettes, muted leathers, and soft atmospheric photography.",
            [
                new("Muted ranger palette", "Greens, greys, and dusty bone with almost no saturated accents. The whole piece feels quieter, which suits the sculpt."),
                new("Photo backdrop upgrade", "Swapped the bright paper backdrop for warm grey card. It fixes a lot of the color cast in phone photos.")
            ]),
        new(
            "cinderfox",
            "cinderfox@example.test",
            "Palette123!",
            "Avery Lin",
            new[] { "User" },
            "Paints fast-moving skirmish warbands with bold spot colors and readable tabletop contrast.",
            [
                new("Warband sprint update", "Finished six models in four evenings by locking the recipe early and refusing to chase tiny corrections."),
                new("Orange spot color test", "A single saturated orange sash did more for model readability than another pass of edge highlights.")
            ]),
        new(
            "oakpigment",
            "oakpigment@example.test",
            "Palette123!",
            "Graham Holt",
            new[] { "User" },
            "Historical painter obsessed with worn canvas, dusty boots, and believable campaign grime.",
            [
                new("Campaign dust on greatcoats", "Used a soft tan pigment around hems and elbows, then fixed it lightly so the folds stayed visible."),
                new("Command stand complete", "Kept the officer cleaner than the rank-and-file so the eye lands there first without needing a brighter uniform.")
            ]),
        new(
            "lumenforge",
            "lumenforge@example.test",
            "Palette123!",
            "Mira Sol",
            new[] { "User" },
            "Builds luminous magical effects, moonlit skin tones, and glowing runes for fantasy showcase pieces.",
            [
                new("Moonlit skin experiment", "Mixed a touch of blue into the midtone instead of only the shadows. It shifted the whole model into night lighting."),
                new("Runes with less chalkiness", "Stopped drybrushing the glow. Thin glazes over a tight white base are cleaner and easier to control.")
            ])
    ];

    private static readonly HashSet<string> AllowedAvatarExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IImageService _imageService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DevelopmentContentSeeder> _logger;

    public DevelopmentContentSeeder(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IImageService imageService,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<DevelopmentContentSeeder> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _imageService = imageService;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<DevelopmentSeedResult> ResetAndSeedAsync(string avatarsDirectory, CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException("Development content seeding is only supported in the Development environment.");
        }

        if (string.IsNullOrWhiteSpace(avatarsDirectory))
        {
            throw new ArgumentException("An avatar source directory is required.", nameof(avatarsDirectory));
        }

        var avatarFiles = GetAvatarFiles(avatarsDirectory);

        await ResetDatabaseAsync(ct);
        PurgeLocalImageStorage();
        await EnsureRolesAsync();

        var now = DateTime.UtcNow;
        var avatarUrls = await UploadAvatarsAsync(avatarFiles, ct);
        var usersByUserName = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < SeedUsers.Count; i++)
        {
            var seedUser = SeedUsers[i];
            var avatarUrl = avatarUrls[i];
            var joinedUtc = now.AddDays(-(SeedUsers.Count - i) * 6);

            var user = new ApplicationUser
            {
                UserName = seedUser.UserName,
                Email = seedUser.Email,
                EmailConfirmed = true,
                DisplayName = seedUser.DisplayName,
                AvatarUrl = avatarUrl,
                DateJoined = joinedUtc
            };

            var createResult = await _userManager.CreateAsync(user, seedUser.Password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create dev seed user '{seedUser.UserName}': {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            }

            foreach (var role in seedUser.Roles)
            {
                var roleResult = await _userManager.AddToRoleAsync(user, role);
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to assign role '{role}' to '{seedUser.UserName}': {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");
                }
            }

            usersByUserName[user.UserName!] = user;
            _dbContext.Profiles.Add(new Profile
            {
                UserId = user.Id,
                DisplayName = seedUser.DisplayName,
                Bio = seedUser.Bio,
                AvatarUrl = avatarUrl
            });
        }

        await _dbContext.SaveChangesAsync(ct);

        var posts = CreatePosts(usersByUserName, now);
        _dbContext.Posts.AddRange(posts);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded {UserCount} development users, {PostCount} posts, and {AvatarCount} avatars from {AvatarDirectory}",
            SeedUsers.Count,
            posts.Count,
            avatarUrls.Count,
            avatarsDirectory);

        return new DevelopmentSeedResult(
            SeedUsers.Count,
            posts.Count,
            avatarUrls.Count,
            SeedUsers.Select(u => new SeededUserCredential(u.UserName, u.Password, u.Email, u.Roles)).ToList());
    }

    private async Task ResetDatabaseAsync(CancellationToken ct)
    {
        await _dbContext.Database.EnsureDeletedAsync(ct);

        if (_dbContext.Database.IsRelational())
        {
            await _dbContext.Database.MigrateAsync(ct);
            return;
        }

        await _dbContext.Database.EnsureCreatedAsync(ct);
    }

    private void PurgeLocalImageStorage()
    {
        var location = LocalImageStoragePaths.Resolve(_environment, _configuration);
        if (Directory.Exists(location.PhysicalPath))
        {
            Directory.Delete(location.PhysicalPath, recursive: true);
        }

        Directory.CreateDirectory(location.PhysicalPath);
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var roleName in SeedUsers.SelectMany(u => u.Roles).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }
        }
    }

    private async Task<IReadOnlyList<string>> UploadAvatarsAsync(IReadOnlyList<string> avatarFiles, CancellationToken ct)
    {
        var urls = new List<string>(SeedUsers.Count);

        for (var i = 0; i < SeedUsers.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var seedUser = SeedUsers[i];
            var avatarFile = avatarFiles[i];
            var extension = Path.GetExtension(avatarFile);
            var uploadFileName = $"seed-avatar-{i + 1:00}-{seedUser.UserName}{extension}";

            await using var stream = File.OpenRead(avatarFile);
            urls.Add(await _imageService.UploadAsync(stream, uploadFileName));
        }

        return urls;
    }

    private static IReadOnlyList<string> GetAvatarFiles(string avatarsDirectory)
    {
        var fullPath = Path.GetFullPath(avatarsDirectory);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Avatar source directory '{fullPath}' was not found.");
        }

        var files = Directory.EnumerateFiles(fullPath)
            .Where(file => AllowedAvatarExtensions.Contains(Path.GetExtension(file)))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count < SeedUsers.Count)
        {
            throw new InvalidOperationException(
                $"Avatar source directory '{fullPath}' must contain at least {SeedUsers.Count} PNG/JPG/WEBP files. Found {files.Count}.");
        }

        return files.Take(SeedUsers.Count).ToList();
    }

    private static List<Post> CreatePosts(
        IReadOnlyDictionary<string, ApplicationUser> usersByUserName,
        DateTime now)
    {
        var posts = new List<Post>(SeedUsers.Sum(user => user.Posts.Count));

        for (var userIndex = 0; userIndex < SeedUsers.Count; userIndex++)
        {
            var seedUser = SeedUsers[userIndex];
            var user = usersByUserName[seedUser.UserName];

            for (var postIndex = 0; postIndex < seedUser.Posts.Count; postIndex++)
            {
                var offsetHours = ((SeedUsers.Count - userIndex) * 9) + (postIndex * 3);
                var createdUtc = now.AddHours(-offsetHours);

                posts.Add(new Post
                {
                    CreatedById = user.Id,
                    Title = seedUser.Posts[postIndex].Title,
                    Content = seedUser.Posts[postIndex].Content,
                    CreatedUtc = createdUtc,
                    UpdatedUtc = createdUtc,
                    IsDeleted = false
                });
            }
        }

        return posts;
    }
}

public sealed record DevelopmentSeedResult(
    int UsersCreated,
    int PostsCreated,
    int AvatarsImported,
    IReadOnlyList<SeededUserCredential> Credentials);

public sealed record SeededUserCredential(
    string UserName,
    string Password,
    string Email,
    IReadOnlyList<string> Roles);

internal sealed record DevelopmentSeedUser(
    string UserName,
    string Email,
    string Password,
    string DisplayName,
    IReadOnlyList<string> Roles,
    string Bio,
    IReadOnlyList<DevelopmentSeedPost> Posts);

internal sealed record DevelopmentSeedPost(string Title, string Content);
