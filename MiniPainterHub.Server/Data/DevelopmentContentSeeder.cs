using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Features.Tags;
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
    private const string SeedAvatarFilePrefix = "seed-avatar-";

    private static readonly IReadOnlyList<DevelopmentSeedUser> SeedUsers =
    [
        new(
            "admin",
            "admin@local",
            "Elena Hart",
            new[] { "Admin", "User" },
            "Display painter and studio founder documenting grimdark armies and bright skirmish bands.",
            [
                new("Studio kickoff: 2026 painting goals", "Resetting the hobby desk this week with a focus on cleaner blends, faster basing, and finally finishing the swamp warband.", ["goals", "basing", "grimdark"], "03-overhead-hobby-desk.png"),
                new("WIP: rusted sentinel captain", "Tried a warmer copper undercoat before the turquoise weathering pass. The contrast is much stronger than my last attempt.", ["weathering", "verdigris", "grimdark"], "05-rusted-sentinel.png")
            ]),
        new(
            "user",
            "user@local",
            "Marcus Vale",
            new[] { "User" },
            "Weekend hobbyist painting historical infantry, terrain scatter, and anything with a weathered coat.",
            [
                new("First squad done this month", "Batch painting is finally clicking. I limited the palette to three greens and it kept the whole unit cohesive.", ["batch-painting", "tabletop", "greens"]),
                new("Snow basing experiment", "Used crushed glass, matte medium, and a tiny bit of blue wash. It reads colder in person than in the reference photos.", ["basing", "snow", "winter"], "01-snow-warband.png")
            ]),
        new(
            "studiomod",
            "studiomod@local",
            "Priya Nair",
            new[] { "Moderator", "User" },
            "Community moderator focused on clean tutorials, readable color recipes, and approachable feedback.",
            [
                new("Quick tip: smoother cloaks", "Thin the midtone one step more than you think you need, then glaze shadows back in instead of trying to nail the blend in one pass.", ["glazing", "tutorial", "cloth"], "04-cloak-study.png"),
                new("Palette challenge thread idea", "Thinking about a weekly prompt built around one accent color and one texture goal. Could be a good way to keep posts focused.", ["challenge", "community", "palette"])
            ]),
        new(
            "inkandiron",
            "inkandiron@example.test",
            "Jonah Mercer",
            new[] { "User" },
            "Paints battered sci-fi crews with lots of oil streaking, sponge chipping, and fluorescent lenses.",
            [
                new("Hazard stripes on boarding shields", "Masked the stripes instead of freehanding them. Slower setup, but the end result is crisp enough for close-up shots.", ["hazard-stripes", "masking", "sci-fi"]),
                new("Neon lens recipe", "White ink dot, magenta glaze, then a final hot pink edge. It pops even under flat desk lighting.", ["osl", "lenses", "sci-fi"])
            ]),
        new(
            "velvetwash",
            "velvetwash@example.test",
            "Sofia Klein",
            new[] { "User" },
            "Loves rich velvets, jewel tones, and display-scale fantasy heroes with dramatic capes.",
            [
                new("Burgundy cloak progression", "Started from a deep brown-red and pushed highlights with muted rose instead of pure red. The fabric looks heavier now.", ["cloak", "blending", "display"]),
                new("Display plinth thoughts", "Simple black plinth, matte finish, no nameplate. The miniature needed breathing room more than extra decoration.", ["display", "plinth", "presentation"])
            ]),
        new(
            "brasslantern",
            "brasslantern@example.test",
            "Theo Barrett",
            new[] { "User" },
            "Terrain builder mixing scratch-built ruins, resin debris, and muddy pigments for campaign boards.",
            [
                new("Ruined archway ready for primer", "I added one more broken beam to make the silhouette less symmetrical. It feels more believable now.", ["terrain", "scratchbuild", "ruins"]),
                new("Mud mix that survived varnish", "Pigment plus acrylic paste plus a drop of gloss medium. It kept the wet look after sealing.", ["terrain", "mud", "weathering"])
            ]),
        new(
            "ashenreed",
            "ashenreed@example.test",
            "Nadia Brooks",
            new[] { "User" },
            "Focuses on desaturated fantasy palettes, muted leathers, and soft atmospheric photography.",
            [
                new("Muted ranger palette", "Greens, greys, and dusty bone with almost no saturated accents. The whole piece feels quieter, which suits the sculpt.", ["desaturated", "fantasy", "leather"]),
                new("Photo backdrop upgrade", "Swapped the bright paper backdrop for warm grey card. It fixes a lot of the color cast in phone photos.", ["photography", "backdrop", "display"])
            ]),
        new(
            "cinderfox",
            "cinderfox@example.test",
            "Avery Lin",
            new[] { "User" },
            "Paints fast-moving skirmish warbands with bold spot colors and readable tabletop contrast.",
            [
                new("Warband sprint update", "Finished six models in four evenings by locking the recipe early and refusing to chase tiny corrections.", ["skirmish", "speedpaint", "tabletop"]),
                new("Orange spot color test", "A single saturated orange sash did more for model readability than another pass of edge highlights.", ["spot-color", "contrast", "tabletop"])
            ]),
        new(
            "oakpigment",
            "oakpigment@example.test",
            "Graham Holt",
            new[] { "User" },
            "Historical painter obsessed with worn canvas, dusty boots, and believable campaign grime.",
            [
                new("Campaign dust on greatcoats", "Used a soft tan pigment around hems and elbows, then fixed it lightly so the folds stayed visible.", ["historical", "pigments", "weathering"], "02-historical-squad.png"),
                new("Command stand complete", "Kept the officer cleaner than the rank-and-file so the eye lands there first without needing a brighter uniform.", ["historical", "composition", "display"])
            ]),
        new(
            "lumenforge",
            "lumenforge@example.test",
            "Mira Sol",
            new[] { "User" },
            "Builds luminous magical effects, moonlit skin tones, and glowing runes for fantasy showcase pieces.",
            [
                new("Moonlit skin experiment", "Mixed a touch of blue into the midtone instead of only the shadows. It shifted the whole model into night lighting.", ["moonlight", "skin", "fantasy"]),
                new("Runes with less chalkiness", "Stopped drybrushing the glow. Thin glazes over a tight white base are cleaner and easier to control.", ["runes", "osl", "glazing"])
            ])
    ];

    internal static IReadOnlyDictionary<string, string> SeedPostImageMappings { get; } = SeedUsers
        .SelectMany(user => user.Posts)
        .Where(post => !string.IsNullOrWhiteSpace(post.ImageAssetName))
        .ToDictionary(post => post.Title, post => post.ImageAssetName!, StringComparer.Ordinal);

    private static readonly IReadOnlyList<DevelopmentSeedFollow> SeedFollows =
    [
        new("admin", "user"),
        new("admin", "studiomod"),
        new("admin", "velvetwash"),
        new("user", "admin"),
        new("user", "oakpigment"),
        new("user", "ashenreed"),
        new("studiomod", "admin"),
        new("studiomod", "user"),
        new("studiomod", "velvetwash"),
        new("cinderfox", "inkandiron"),
        new("lumenforge", "velvetwash"),
        new("brasslantern", "admin")
    ];

    private static readonly IReadOnlyList<DevelopmentSeedConversation> SeedConversations =
    [
        new(
            "admin",
            "user",
            [
                new("admin", "Your snow basing note worked well for the winter challenge. Do you have a good photo of the squad?"),
                new("user", "Yes, and I finally got the crushed-glass sparkle under control. I can post the recipe tonight."),
                new("admin", "Perfect. I want something in the seed inbox that looks like real hobby chatter."),
                new("user", "Then leave this one unread on your side so the badge has something to show.")
            ],
            UnreadForUserNames: ["admin"]),
        new(
            "admin",
            "studiomod",
            [
                new("studiomod", "I drafted the weekly palette challenge prompt. Want me to pin it after the evening post rush?"),
                new("admin", "Yes. Keep the brief tight and make the accent-color rule obvious."),
                new("studiomod", "Done. I will queue it after I answer a couple of feedback threads.")
            ],
            UnreadForUserNames: ["admin"]),
        new(
            "user",
            "studiomod",
            [
                new("user", "Could you take a look at my cloak blend before I varnish it?"),
                new("studiomod", "The transitions are good. Push one more midtone glaze across the shoulder and stop there.")
            ],
            UnreadForUserNames: ["user"]),
        new(
            "velvetwash",
            "lumenforge",
            [
                new("velvetwash", "Your moonlit skin recipe made me rethink my shadow colors."),
                new("lumenforge", "Try cooling the midtone, not just the recesses. It keeps the face from going chalky."),
                new("velvetwash", "That should work on the burgundy hero too. I will test it on the next cape.")
            ],
            UnreadForUserNames: [])
    ];

    private static readonly IReadOnlyList<DevelopmentSeedComment> SeedComments =
    [
        new("admin", "Studio kickoff: 2026 painting goals", "user", "That swamp warband lineup looks strong already. The desk reset is going to pay off."),
        new("admin", "Studio kickoff: 2026 painting goals", "studiomod", "The basing plan sounds good. Post the final recipe once you lock the mud tones in."),
        new("admin", "WIP: rusted sentinel captain", "inkandiron", "The copper undercoat is doing exactly what it should here. The corrosion reads much better."),
        new("user", "First squad done this month", "oakpigment", "Keeping the greens tight was the right move. The unit reads like one campaign formation."),
        new("user", "Snow basing experiment", "admin", "The cold blue note is subtle but it lands. This would photograph well as a feature post."),
        new("user", "Snow basing experiment", "ashenreed", "The snow texture looks clean. I would leave it exactly this restrained."),
        new("studiomod", "Quick tip: smoother cloaks", "velvetwash", "This is the kind of reminder people need before they overwork a blend."),
        new("studiomod", "Palette challenge thread idea", "cinderfox", "One accent color plus one texture goal is a good constraint. I would join that thread."),
        new("inkandiron", "Hazard stripes on boarding shields", "cinderfox", "Masked stripes always win on units. The repetition makes the whole group feel sharper."),
        new("inkandiron", "Neon lens recipe", "lumenforge", "That white core keeps the glow crisp. It is much cleaner than broad drybrushed light."),
        new("velvetwash", "Burgundy cloak progression", "studiomod", "The muted rose highlight keeps it rich without turning pink. Strong call."),
        new("brasslantern", "Mud mix that survived varnish", "admin", "The sheen survived exactly enough. It still reads like terrain instead of resin gloss."),
        new("ashenreed", "Photo backdrop upgrade", "user", "Warm grey card really does fix half the battle with phone photos."),
        new("cinderfox", "Orange spot color test", "studiomod", "This is a perfect readability example. The accent carries from across the table."),
        new("oakpigment", "Command stand complete", "user", "Keeping the officer cleaner was enough. The eye lands there without breaking the palette."),
        new("lumenforge", "Runes with less chalkiness", "velvetwash", "Thin glazes over a white base are slower, but the effect looks much more deliberate.")
    ];

    private static readonly HashSet<string> AllowedSeedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
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
    private readonly IImageProcessor _imageProcessor;
    private readonly IImageStore _imageStore;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DevelopmentContentSeeder> _logger;

    public DevelopmentContentSeeder(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IImageService imageService,
        IImageProcessor imageProcessor,
        IImageStore imageStore,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<DevelopmentContentSeeder> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _imageService = imageService;
        _imageProcessor = imageProcessor;
        _imageStore = imageStore;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<DevelopmentSeedResult> ResetAndSeedAsync(
        string avatarsDirectory,
        string? postImagesDirectory = null,
        CancellationToken ct = default)
    {
        EnsureDevelopmentEnvironment();
        ValidateSeedPostsHaveTags();
        var avatarFiles = GetAvatarFiles(avatarsDirectory);
        var postImageFiles = GetPostImageFiles(postImagesDirectory);

        await ResetDatabaseAsync(ct);
        PrepareLocalImageStorage(clearAllFiles: true);
        await EnsureRolesAsync();

        var now = DateTime.UtcNow;
        var credentials = DevelopmentSeedCredentials.FromConfiguration(_configuration);
        var avatars = await ImportAvatarsAsync(avatarFiles, ct);
        var usersByUserName = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < SeedUsers.Count; i++)
        {
            var seedUser = SeedUsers[i];
            var avatarUrl = avatars[i].AvatarUrl;
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

            var createResult = await _userManager.CreateAsync(user, credentials.GetPassword(seedUser.UserName));
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
        var hobbyProjects = CreateHobbyProjects(posts, usersByUserName, now);
        _dbContext.HobbyProjects.AddRange(hobbyProjects);
        var commentsCreated = CreateComments(posts, usersByUserName);
        _dbContext.Comments.AddRange(commentsCreated);
        var followsCreated = CreateFollows(usersByUserName, now);
        _dbContext.Follows.AddRange(followsCreated);
        var socialSeedResult = CreateConversations(usersByUserName, now);
        _dbContext.Conversations.AddRange(socialSeedResult.Conversations);
        await _dbContext.SaveChangesAsync(ct);
        var postImagesImported = await ImportPostImagesAsync(posts, postImageFiles, ct);

        if (postImagesImported > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Seeded {UserCount} development users, {PostCount} posts, {ProjectCount} hobby projects, {CommentCount} comments, {AvatarCount} avatars, {FollowCount} follow relationships, {ConversationCount} conversations, {MessageCount} direct messages, and {PostImageCount} post images from avatar source {AvatarDirectory}{PostImageSuffix}",
            SeedUsers.Count,
            posts.Count,
            hobbyProjects.Count,
            commentsCreated.Count,
            avatars.Count,
            followsCreated.Count,
            socialSeedResult.Conversations.Count,
            socialSeedResult.MessageCount,
            postImagesImported,
            avatarsDirectory,
            string.IsNullOrWhiteSpace(postImagesDirectory)
                ? string.Empty
                : $" using post image source {postImagesDirectory}");

        return new DevelopmentSeedResult(
            SeedUsers.Count,
            posts.Count,
            commentsCreated.Count,
            avatars.Count,
            postImagesImported,
            SeedUsers.Select(u => new SeededUserCredential(u.UserName, credentials.GetPassword(u.UserName), u.Email, u.Roles)).ToList());
    }

    public async Task<DevelopmentAvatarGenerationResult> GenerateAvatarsOnlyAsync(string avatarsDirectory, CancellationToken ct = default)
    {
        EnsureDevelopmentEnvironment();
        ValidateSeedPostsHaveTags();
        var avatarFiles = GetAvatarFiles(avatarsDirectory);

        PrepareLocalImageStorage(clearAllFiles: false);
        var avatars = await ImportAvatarsAsync(avatarFiles, ct);
        var existingUsersUpdated = await ApplyAvatarsToExistingSeedUsersAsync(avatars, ct);

        _logger.LogInformation(
            "Generated {AvatarCount} development avatars from {AvatarDirectory}; updated {UpdatedUserCount} existing seed users.",
            avatars.Count,
            avatarsDirectory,
            existingUsersUpdated);

        return new DevelopmentAvatarGenerationResult(
            avatars.Count,
            existingUsersUpdated,
            avatars);
    }

    private void EnsureDevelopmentEnvironment()
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException("Development content seeding is only supported in the Development environment.");
        }
    }

    private static void ValidateSeedPostsHaveTags()
    {
        foreach (var seedUser in SeedUsers)
        {
            foreach (var seedPost in seedUser.Posts)
            {
                var validTags = seedPost.Tags
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .ToList();

                if (validTags.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Development seed post '{seedPost.Title}' for '{seedUser.UserName}' must define at least one tag.");
                }
            }
        }
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

    private void PrepareLocalImageStorage(bool clearAllFiles)
    {
        var location = LocalImageStoragePaths.Resolve(_environment, _configuration);

        if (clearAllFiles)
        {
            if (Directory.Exists(location.PhysicalPath))
            {
                Directory.Delete(location.PhysicalPath, recursive: true);
            }

            Directory.CreateDirectory(location.PhysicalPath);
            return;
        }

        Directory.CreateDirectory(location.PhysicalPath);

        foreach (var file in Directory.EnumerateFiles(location.PhysicalPath, $"{SeedAvatarFilePrefix}*", SearchOption.TopDirectoryOnly))
        {
            File.Delete(file);
        }
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

    private async Task<IReadOnlyList<SeededAvatarAsset>> ImportAvatarsAsync(IReadOnlyList<string> avatarFiles, CancellationToken ct)
    {
        var avatars = new List<SeededAvatarAsset>(SeedUsers.Count);

        for (var i = 0; i < SeedUsers.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var seedUser = SeedUsers[i];
            var avatarFile = avatarFiles[i];
            var extension = Path.GetExtension(avatarFile);
            var uploadFileName = $"{SeedAvatarFilePrefix}{i + 1:00}-{seedUser.UserName}{extension}";

            await using var stream = File.OpenRead(avatarFile);
            var avatarUrl = await _imageService.UploadAsync(stream, uploadFileName);
            avatars.Add(new SeededAvatarAsset(
                seedUser.UserName,
                seedUser.DisplayName,
                Path.GetFileName(avatarFile),
                avatarUrl));
        }

        return avatars;
    }

    private async Task<int> ImportPostImagesAsync(
        IReadOnlyList<Post> posts,
        IReadOnlyDictionary<string, string> postImageFiles,
        CancellationToken ct)
    {
        if (postImageFiles.Count == 0)
        {
            return 0;
        }

        var postsByTitle = posts
            .Where(post => SeedPostImageMappings.ContainsKey(post.Title))
            .ToDictionary(post => post.Title, StringComparer.Ordinal);
        var imported = 0;

        foreach (var mapping in SeedPostImageMappings)
        {
            ct.ThrowIfCancellationRequested();

            if (!postsByTitle.TryGetValue(mapping.Key, out var post))
            {
                throw new InvalidOperationException($"Seed post '{mapping.Key}' was not created for image asset '{mapping.Value}'.");
            }

            if (!postImageFiles.TryGetValue(mapping.Value, out var postImageFile))
            {
                throw CreateMissingPostImageException(mapping.Key, mapping.Value);
            }

            await using var stream = File.OpenRead(postImageFile);
            var variants = await _imageProcessor.ProcessAsync(stream, contentType: null, ct);
            var stored = await _imageStore.SaveAsync(ConvertToStorageGuid(post.Id), Guid.NewGuid(), variants, ct);
            post.Images.Add(new PostImage
            {
                PostId = post.Id,
                ImageUrl = stored.MaxUrl,
                PreviewUrl = stored.PreviewUrl,
                ThumbnailUrl = stored.ThumbUrl,
                Width = variants.Max.Width,
                Height = variants.Max.Height
            });
            imported++;
        }

        return imported;
    }

    private static Guid ConvertToStorageGuid(int postId) =>
        new(postId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private async Task<int> ApplyAvatarsToExistingSeedUsersAsync(IReadOnlyList<SeededAvatarAsset> avatars, CancellationToken ct)
    {
        var userNames = avatars.Select(avatar => avatar.UserName).ToArray();
        var users = await _dbContext.Users
            .OfType<ApplicationUser>()
            .Where(user => user.UserName != null && userNames.Contains(user.UserName))
            .ToListAsync(ct);

        if (users.Count == 0)
        {
            return 0;
        }

        var userIds = users.Select(user => user.Id).ToArray();
        var profiles = await _dbContext.Profiles
            .Where(profile => userIds.Contains(profile.UserId))
            .ToListAsync(ct);

        var usersByUserName = users.ToDictionary(user => user.UserName!, StringComparer.OrdinalIgnoreCase);
        var profilesByUserId = profiles.ToDictionary(profile => profile.UserId, StringComparer.Ordinal);
        var updatedUsers = 0;

        foreach (var avatar in avatars)
        {
            if (!usersByUserName.TryGetValue(avatar.UserName, out var user))
            {
                continue;
            }

            updatedUsers++;
            user.AvatarUrl = avatar.AvatarUrl;

            if (profilesByUserId.TryGetValue(user.Id, out var profile))
            {
                profile.AvatarUrl = avatar.AvatarUrl;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return updatedUsers;
    }

    private static IReadOnlyList<string> GetAvatarFiles(string avatarsDirectory)
    {
        if (string.IsNullOrWhiteSpace(avatarsDirectory))
        {
            throw new ArgumentException("An avatar source directory is required.", nameof(avatarsDirectory));
        }

        var fullPath = Path.GetFullPath(avatarsDirectory);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Avatar source directory '{fullPath}' was not found.");
        }

        var files = Directory.EnumerateFiles(fullPath)
            .Where(file => AllowedSeedImageExtensions.Contains(Path.GetExtension(file)))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count < SeedUsers.Count)
        {
            throw new InvalidOperationException(
                $"Avatar source directory '{fullPath}' must contain at least {SeedUsers.Count} PNG/JPG/WEBP files. Found {files.Count}.");
        }

        return files.Take(SeedUsers.Count).ToList();
    }

    private static IReadOnlyDictionary<string, string> GetPostImageFiles(string? postImagesDirectory)
    {
        if (string.IsNullOrWhiteSpace(postImagesDirectory))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var fullPath = Path.GetFullPath(postImagesDirectory);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Post image source directory '{fullPath}' was not found.");
        }

        var files = Directory.EnumerateFiles(fullPath)
            .Where(file => AllowedSeedImageExtensions.Contains(Path.GetExtension(file)))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .GroupBy(file => Path.GetFileName(file)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in SeedPostImageMappings)
        {
            if (!files.ContainsKey(mapping.Value))
            {
                throw CreateMissingPostImageException(mapping.Key, mapping.Value, fullPath);
            }
        }

        return files;
    }

    private static InvalidOperationException CreateMissingPostImageException(
        string postTitle,
        string imageAssetName,
        string? sourceDirectory = null)
    {
        var location = string.IsNullOrWhiteSpace(sourceDirectory)
            ? string.Empty
            : $" in '{sourceDirectory}'";

        return new InvalidOperationException(
            $"Development seed post '{postTitle}' declares image asset '{imageAssetName}', but that file was not found{location}.");
    }

    private static List<Post> CreatePosts(
        IReadOnlyDictionary<string, ApplicationUser> usersByUserName,
        DateTime now)
    {
        var posts = new List<Post>(SeedUsers.Sum(user => user.Posts.Count));
        var tagsByNormalizedName = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);
        var usedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var userIndex = 0; userIndex < SeedUsers.Count; userIndex++)
        {
            var seedUser = SeedUsers[userIndex];
            var user = usersByUserName[seedUser.UserName];

            for (var postIndex = 0; postIndex < seedUser.Posts.Count; postIndex++)
            {
                var seedPost = seedUser.Posts[postIndex];
                var offsetHours = ((SeedUsers.Count - userIndex) * 9) + (postIndex * 3);
                var createdUtc = now.AddHours(-offsetHours);
                var post = new Post
                {
                    CreatedById = user.Id,
                    Title = seedPost.Title,
                    Content = seedPost.Content,
                    CreatedUtc = createdUtc,
                    UpdatedUtc = createdUtc,
                    IsDeleted = false
                };

                AttachTags(post, seedPost.Tags, tagsByNormalizedName, usedSlugs, createdUtc);
                posts.Add(post);
            }
        }

        return posts;
    }

    private static void AttachTags(
        Post post,
        IReadOnlyList<string> tagNames,
        IDictionary<string, Tag> tagsByNormalizedName,
        ISet<string> usedSlugs,
        DateTime createdUtc)
    {
        foreach (var tagName in tagNames)
        {
            var displayName = TagTextUtilities.CollapseWhitespace(tagName);
            var normalizedName = TagTextUtilities.NormalizeText(displayName);

            if (!tagsByNormalizedName.TryGetValue(normalizedName, out var tag))
            {
                var baseSlug = TagTextUtilities.CreateSlug(displayName);
                var slug = TagTextUtilities.ResolveUniqueSlug(baseSlug, usedSlugs);
                tag = new Tag
                {
                    DisplayName = displayName,
                    NormalizedName = normalizedName,
                    Slug = slug,
                    CreatedUtc = createdUtc
                };

                tagsByNormalizedName[normalizedName] = tag;
                usedSlugs.Add(slug);
            }

            post.PostTags.Add(new PostTag
            {
                Post = post,
                Tag = tag
            });
        }
    }

    private static List<HobbyProject> CreateHobbyProjects(
        IReadOnlyList<Post> posts,
        IReadOnlyDictionary<string, ApplicationUser> usersByUserName,
        DateTime now)
    {
        Post FindPost(string ownerUserName, string title)
        {
            var owner = usersByUserName[ownerUserName];
            return posts.Single(post =>
                string.Equals(post.CreatedById, owner.Id, StringComparison.Ordinal)
                && string.Equals(post.Title, title, StringComparison.Ordinal));
        }

        HobbyProjectEntry Entry(HobbyProject project, Post post, string? milestone, int? showcaseOrder) =>
            new()
            {
                Project = project,
                Post = post,
                PostId = post.Id,
                LinkedUtc = post.CreatedUtc.AddMinutes(5),
                MilestoneLabel = milestone,
                ShowcaseOrder = showcaseOrder
            };

        var admin = usersByUserName["admin"];
        var kickoff = FindPost("admin", "Studio kickoff: 2026 painting goals");
        var sentinel = FindPost("admin", "WIP: rusted sentinel captain");
        var sentinelProject = new HobbyProject
        {
            OwnerUserId = admin.Id,
            OwnerUser = admin,
            Title = "Rusted Sentinel Cohort",
            Description = "A weathered sci-fi force built around warm copper, turquoise corrosion, and swamp basing.",
            Kind = HobbyProjectKinds.Army,
            GameSystem = "Grimdark sci-fi",
            FactionTheme = "Rusted sentinels",
            Goal = "Finish a cohesive display cohort while documenting the repeatable weathering recipe.",
            StartDate = DateOnly.FromDateTime(now.AddDays(-90)),
            Status = HobbyProjectStatuses.InProgress,
            CoverPostId = sentinel.Id,
            CoverPost = sentinel,
            CreatedUtc = kickoff.CreatedUtc.AddHours(-2),
            UpdatedUtc = sentinel.UpdatedUtc
        };
        sentinelProject.Entries.Add(Entry(sentinelProject, kickoff, "Project kickoff", null));
        sentinelProject.Entries.Add(Entry(sentinelProject, sentinel, "First captain painted", 1));

        var user = usersByUserName["user"];
        var firstSquad = FindPost("user", "First squad done this month");
        var snowBasing = FindPost("user", "Snow basing experiment");
        var winterProject = new HobbyProject
        {
            OwnerUserId = user.Id,
            OwnerUser = user,
            Title = "Winter Infantry Company",
            Description = "A completed winter force using a restricted green palette and cold crushed-glass snow bases.",
            Kind = HobbyProjectKinds.Army,
            GameSystem = "Historical tabletop",
            FactionTheme = "Winter campaign",
            Goal = "Complete a table-ready company with consistent greens and snow texture.",
            StartDate = DateOnly.FromDateTime(now.AddDays(-120)),
            Status = HobbyProjectStatuses.Completed,
            CoverPostId = snowBasing.Id,
            CoverPost = snowBasing,
            CreatedUtc = firstSquad.CreatedUtc.AddDays(-30),
            UpdatedUtc = snowBasing.UpdatedUtc,
            CompletedUtc = snowBasing.UpdatedUtc
        };
        winterProject.Entries.Add(Entry(winterProject, firstSquad, "First squad complete", 1));
        winterProject.Entries.Add(Entry(winterProject, snowBasing, "Winter basing locked", 2));

        var terrainBuilder = usersByUserName["brasslantern"];
        var archway = FindPost("brasslantern", "Ruined archway ready for primer");
        var mudMix = FindPost("brasslantern", "Mud mix that survived varnish");
        var terrainProject = new HobbyProject
        {
            OwnerUserId = terrainBuilder.Id,
            OwnerUser = terrainBuilder,
            Title = "Campaign Ruins Board",
            Description = "A modular board of broken arches, resin debris, and durable wet mud for a narrative campaign.",
            Kind = HobbyProjectKinds.Terrain,
            FactionTheme = "Abandoned campaign ruins",
            Goal = "Build six matching terrain modules for a compact campaign table.",
            StartDate = DateOnly.FromDateTime(now.AddDays(-45)),
            Status = HobbyProjectStatuses.InProgress,
            CreatedUtc = archway.CreatedUtc.AddDays(-7),
            UpdatedUtc = mudMix.UpdatedUtc
        };
        terrainProject.Entries.Add(Entry(terrainProject, archway, "First silhouette approved", 1));
        terrainProject.Entries.Add(Entry(terrainProject, mudMix, "Mud recipe finalized", 2));

        return new List<HobbyProject> { sentinelProject, winterProject, terrainProject };
    }

    private static List<Comment> CreateComments(
        IReadOnlyList<Post> posts,
        IReadOnlyDictionary<string, ApplicationUser> usersByUserName)
    {
        var comments = new List<Comment>(SeedComments.Count);

        for (var i = 0; i < SeedComments.Count; i++)
        {
            var seedComment = SeedComments[i];
            var postOwner = usersByUserName[seedComment.PostOwnerUserName];
            var author = usersByUserName[seedComment.CommentAuthorUserName];
            var post = posts.SingleOrDefault(candidate =>
                string.Equals(candidate.CreatedById, postOwner.Id, StringComparison.Ordinal)
                && string.Equals(candidate.Title, seedComment.PostTitle, StringComparison.Ordinal));

            if (post is null)
            {
                throw new InvalidOperationException(
                    $"Seed comment target post '{seedComment.PostTitle}' for '{seedComment.PostOwnerUserName}' was not created.");
            }

            if (string.Equals(post.CreatedById, author.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Seed comment author '{seedComment.CommentAuthorUserName}' cannot comment on their own seeded post '{seedComment.PostTitle}'.");
            }

            var createdUtc = post.CreatedUtc.AddMinutes((i % 3 + 1) * 11);
            comments.Add(new Comment
            {
                PostId = post.Id,
                AuthorId = author.Id,
                Text = seedComment.Text,
                CreatedUtc = createdUtc,
                UpdatedUtc = createdUtc,
                IsDeleted = false
            });
        }

        return comments;
    }

    private static List<Follow> CreateFollows(
        IReadOnlyDictionary<string, ApplicationUser> usersByUserName,
        DateTime now)
    {
        var follows = new List<Follow>(SeedFollows.Count);

        for (var i = 0; i < SeedFollows.Count; i++)
        {
            var seedFollow = SeedFollows[i];
            follows.Add(new Follow
            {
                FollowerUserId = usersByUserName[seedFollow.FollowerUserName].Id,
                FollowedUserId = usersByUserName[seedFollow.FollowedUserName].Id,
                CreatedUtc = now.AddHours(-((SeedFollows.Count - i) * 4))
            });
        }

        return follows;
    }

    private static SeededConversationBatch CreateConversations(
        IReadOnlyDictionary<string, ApplicationUser> usersByUserName,
        DateTime now)
    {
        var conversations = new List<Conversation>(SeedConversations.Count);
        var totalMessages = 0;

        for (var i = 0; i < SeedConversations.Count; i++)
        {
            var seedConversation = SeedConversations[i];
            var firstUser = usersByUserName[seedConversation.FirstUserName];
            var secondUser = usersByUserName[seedConversation.SecondUserName];
            var baseUtc = now.AddHours(-((SeedConversations.Count - i) * 6));

            var conversation = new Conversation
            {
                CreatedUtc = baseUtc,
                UpdatedUtc = baseUtc,
                Participants = new List<ConversationParticipant>
                {
                    new()
                    {
                        UserId = firstUser.Id,
                        JoinedUtc = baseUtc,
                        LastReadMessageUtc = null
                    },
                    new()
                    {
                        UserId = secondUser.Id,
                        JoinedUtc = baseUtc,
                        LastReadMessageUtc = null
                    }
                },
                Messages = new List<DirectMessage>()
            };

            DateTime? lastMessageUtc = null;
            var lastMessageByUserName = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase)
            {
                [seedConversation.FirstUserName] = null,
                [seedConversation.SecondUserName] = null
            };

            for (var messageIndex = 0; messageIndex < seedConversation.Messages.Count; messageIndex++)
            {
                var seedMessage = seedConversation.Messages[messageIndex];
                var sender = usersByUserName[seedMessage.SenderUserName];
                var sentUtc = baseUtc.AddMinutes((messageIndex + 1) * 9);
                var message = new DirectMessage
                {
                    SenderUserId = sender.Id,
                    Body = seedMessage.Body,
                    SentUtc = sentUtc
                };

                conversation.Messages.Add(message);
                conversation.UpdatedUtc = sentUtc;
                lastMessageUtc = sentUtc;
                lastMessageByUserName[seedMessage.SenderUserName] = sentUtc;
                totalMessages++;
            }

            foreach (var participant in conversation.Participants)
            {
                var participantUserName = string.Equals(participant.UserId, firstUser.Id, StringComparison.Ordinal)
                    ? seedConversation.FirstUserName
                    : seedConversation.SecondUserName;

                participant.LastReadMessageUtc = seedConversation.UnreadForUserNames.Contains(participantUserName, StringComparer.OrdinalIgnoreCase)
                    ? lastMessageByUserName.TryGetValue(participantUserName, out var ownLastMessageUtc)
                        ? ownLastMessageUtc
                        : null
                    : lastMessageUtc;
            }

            conversations.Add(conversation);
        }

        return new SeededConversationBatch(conversations, totalMessages);
    }
}

public sealed record DevelopmentSeedResult(
    int UsersCreated,
    int PostsCreated,
    int CommentsCreated,
    int AvatarsImported,
    int PostImagesImported,
    IReadOnlyList<SeededUserCredential> Credentials);

public sealed record DevelopmentAvatarGenerationResult(
    int AvatarsImported,
    int ExistingUsersUpdated,
    IReadOnlyList<SeededAvatarAsset> Avatars);

public sealed record SeededUserCredential(
    string UserName,
    string Password,
    string Email,
    IReadOnlyList<string> Roles);

public sealed record SeededAvatarAsset(
    string UserName,
    string DisplayName,
    string SourceFileName,
    string AvatarUrl);

internal sealed record DevelopmentSeedCredentials(
    string AdminPassword,
    string UserPassword,
    string ModeratorPassword,
    string PortfolioUserPassword)
{
    public static DevelopmentSeedCredentials FromConfiguration(IConfiguration configuration)
    {
        return new DevelopmentSeedCredentials(
            GetRequired(configuration, "DevelopmentSeedCredentials:AdminPassword"),
            GetRequired(configuration, "DevelopmentSeedCredentials:UserPassword"),
            GetRequired(configuration, "DevelopmentSeedCredentials:ModeratorPassword"),
            GetRequired(configuration, "DevelopmentSeedCredentials:PortfolioUserPassword"));
    }

    public string GetPassword(string userName) => userName switch
    {
        "admin" => AdminPassword,
        "user" => UserPassword,
        "studiomod" => ModeratorPassword,
        _ => PortfolioUserPassword
    };

    private static string GetRequired(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required development seed credential '{key}'.");
        }

        return value;
    }
}

internal sealed record DevelopmentSeedUser(
    string UserName,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    string Bio,
    IReadOnlyList<DevelopmentSeedPost> Posts);

internal sealed record DevelopmentSeedPost(
    string Title,
    string Content,
    IReadOnlyList<string> Tags,
    string? ImageAssetName = null);

internal sealed record DevelopmentSeedFollow(string FollowerUserName, string FollowedUserName);

internal sealed record DevelopmentSeedConversation(
    string FirstUserName,
    string SecondUserName,
    IReadOnlyList<DevelopmentSeedMessage> Messages,
    IReadOnlyList<string> UnreadForUserNames);

internal sealed record DevelopmentSeedMessage(string SenderUserName, string Body);

internal sealed record DevelopmentSeedComment(
    string PostOwnerUserName,
    string PostTitle,
    string CommentAuthorUserName,
    string Text);

internal sealed record SeededConversationBatch(IReadOnlyList<Conversation> Conversations, int MessageCount);
