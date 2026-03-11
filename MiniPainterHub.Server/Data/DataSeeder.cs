using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MiniPainterHub.Server.Data
{
    /// <summary>
    /// Encapsulates database seeding logic for default users, roles, and baseline posts.
    /// </summary>
    public static class DataSeeder
    {
        private static readonly IReadOnlyList<SeedPostDefinition> SeedPosts =
        [
            new(
                "admin",
                "Seeded: glazing check",
                "Baseline seeded post to verify tag rendering on the home feed.",
                ["glazing", "nmm", "seeded"],
                "seeded-glazing-check.png"),
            new(
                "user",
                "Seeded: weathering notes",
                "Second baseline seeded post for search and tag aggregation checks.",
                ["weathering", "battle-damage", "seeded"],
                null)
        ];

        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();

            const string adminRole = "Admin";
            const string userRole = "User";
            const string moderatorRole = "Moderator";

            await EnsureRoleExistsAsync(roleManager, adminRole);
            await EnsureRoleExistsAsync(roleManager, userRole);
            await EnsureRoleExistsAsync(roleManager, moderatorRole);

            await EnsureUserAsync(
                userManager,
                userName: "admin",
                email: "admin@local",
                password: "P@ssw0rd!",
                requiredRoles: new[] { adminRole, userRole });

            await EnsureUserAsync(
                userManager,
                userName: "user",
                email: "user@local",
                password: "User123!",
                requiredRoles: new[] { userRole });

            await EnsurePostsAsync(db, userManager, imageService);
        }

        public static async Task SeedAdminAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            if (!cfg.GetValue<bool>("SeedAdmin:Enabled"))
            {
                return;
            }

            var adminRole = cfg["SeedAdmin:Role"] ?? "Admin";
            if (await roles.RoleExistsAsync(adminRole))
            {
                var anyAdmin = (await users.GetUsersInRoleAsync(adminRole)).Any();
                if (anyAdmin)
                {
                    return;
                }
            }

            var email = cfg["SeedAdmin:Email"];
            var pwd = cfg["SeedAdmin:Password"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pwd))
            {
                return;
            }

            if (!await roles.RoleExistsAsync(adminRole))
            {
                await roles.CreateAsync(new IdentityRole(adminRole));
            }

            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                var create = await users.CreateAsync(user, pwd);
                if (!create.Succeeded)
                {
                    throw new Exception("Admin seed failed: " + string.Join("; ", create.Errors.Select(e => e.Description)));
                }
            }

            if (!await users.IsInRoleAsync(user, adminRole))
            {
                await users.AddToRoleAsync(user, adminRole);
            }
        }

        private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                return;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create role '{roleName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }
        }

        private static async Task EnsureUserAsync(
            UserManager<ApplicationUser> userManager,
            string userName,
            string email,
            string password,
            IReadOnlyCollection<string> requiredRoles)
        {
            var user = await userManager.FindByNameAsync(userName) ?? await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true
                };

                var create = await userManager.CreateAsync(user, password);
                if (!create.Succeeded)
                {
                    throw new Exception($"Seed user '{userName}' failed: {string.Join("; ", create.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                var requiresUpdate = false;
                if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    user.Email = email;
                    user.NormalizedEmail = userManager.NormalizeEmail(email);
                    requiresUpdate = true;
                }

                if (!string.Equals(user.UserName, userName, StringComparison.Ordinal))
                {
                    user.UserName = userName;
                    user.NormalizedUserName = userManager.NormalizeName(userName);
                    requiresUpdate = true;
                }

                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    requiresUpdate = true;
                }

                if (requiresUpdate)
                {
                    var update = await userManager.UpdateAsync(user);
                    if (!update.Succeeded)
                    {
                        throw new Exception($"Seed user '{userName}' update failed: {string.Join("; ", update.Errors.Select(e => e.Description))}");
                    }
                }

                var hasExpectedPassword = await userManager.CheckPasswordAsync(user, password);
                if (!hasExpectedPassword)
                {
                    var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                    var reset = await userManager.ResetPasswordAsync(user, resetToken, password);
                    if (!reset.Succeeded)
                    {
                        throw new Exception($"Seed user '{userName}' password reset failed: {string.Join("; ", reset.Errors.Select(e => e.Description))}");
                    }
                }
            }

            foreach (var role in requiredRoles)
            {
                if (!await userManager.IsInRoleAsync(user, role))
                {
                    var addRole = await userManager.AddToRoleAsync(user, role);
                    if (!addRole.Succeeded)
                    {
                        throw new Exception($"Seed user '{userName}' role assignment failed for '{role}': {string.Join("; ", addRole.Errors.Select(e => e.Description))}");
                    }
                }
            }
        }

        private static async Task EnsurePostsAsync(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IImageService imageService)
        {
            var existingTags = await db.Tags.ToListAsync();
            var tagsByNormalizedName = existingTags.ToDictionary(tag => tag.NormalizedName, StringComparer.OrdinalIgnoreCase);
            var usedSlugs = existingTags.Select(tag => tag.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var seedPost in SeedPosts)
            {
                var user = await userManager.FindByNameAsync(seedPost.UserName);
                if (user is null)
                {
                    throw new Exception($"Seed post user '{seedPost.UserName}' was not found.");
                }

                var post = await db.Posts
                    .Include(candidate => candidate.Images)
                    .Include(candidate => candidate.PostTags)
                    .ThenInclude(postTag => postTag.Tag)
                    .FirstOrDefaultAsync(candidate =>
                        candidate.CreatedById == user.Id
                        && candidate.Title == seedPost.Title);

                if (post is null)
                {
                    post = new Post
                    {
                        CreatedById = user.Id,
                        Title = seedPost.Title,
                        Content = seedPost.Content,
                        CreatedUtc = DateTime.UtcNow,
                        UpdatedUtc = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    db.Posts.Add(post);
                }
                else
                {
                    post.Content = seedPost.Content;
                    post.IsDeleted = false;
                    post.SoftDeletedUtc = null;
                    post.UpdatedUtc = DateTime.UtcNow;
                }

                SyncPostTags(db, post, seedPost.Tags, tagsByNormalizedName, usedSlugs);
                await SyncPostImageAsync(db, post, imageService, seedPost.ImageFileName);
            }

            await db.SaveChangesAsync();
        }

        private static void SyncPostTags(
            AppDbContext db,
            Post post,
            IReadOnlyList<string> tagNames,
            IDictionary<string, Tag> tagsByNormalizedName,
            ISet<string> usedSlugs)
        {
            var desiredNormalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var tagName in tagNames)
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    continue;
                }

                var displayName = TagTextUtilities.CollapseWhitespace(tagName);
                var normalizedName = TagTextUtilities.NormalizeText(displayName);
                if (!desiredNormalizedNames.Add(normalizedName))
                {
                    continue;
                }

                if (!tagsByNormalizedName.TryGetValue(normalizedName, out var tag))
                {
                    var baseSlug = TagTextUtilities.CreateSlug(displayName);
                    if (string.IsNullOrWhiteSpace(baseSlug))
                    {
                        continue;
                    }

                    var slug = ResolveUniqueSlug(baseSlug, usedSlugs);
                    tag = new Tag
                    {
                        DisplayName = displayName,
                        NormalizedName = normalizedName,
                        Slug = slug,
                        CreatedUtc = DateTime.UtcNow
                    };

                    tagsByNormalizedName[normalizedName] = tag;
                    usedSlugs.Add(slug);
                }

                var alreadyLinked = post.PostTags.Any(postTag =>
                    string.Equals(postTag.Tag.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase));
                if (alreadyLinked)
                {
                    continue;
                }

                post.PostTags.Add(new PostTag
                {
                    Post = post,
                    Tag = tag
                });
            }

            var toRemove = post.PostTags
                .Where(postTag => !desiredNormalizedNames.Contains(postTag.Tag.NormalizedName))
                .ToList();
            foreach (var postTag in toRemove)
            {
                post.PostTags.Remove(postTag);
                db.PostTags.Remove(postTag);
            }
        }

        private static string ResolveUniqueSlug(string baseSlug, ISet<string> usedSlugs)
        {
            var candidate = baseSlug;
            var suffix = 2;
            while (usedSlugs.Contains(candidate))
            {
                candidate = $"{baseSlug}-{suffix}";
                suffix++;
            }

            return candidate;
        }

        private static async Task SyncPostImageAsync(
            AppDbContext db,
            Post post,
            IImageService imageService,
            string? imageFileName)
        {
            var orderedImages = post.Images
                .OrderBy(image => image.Id)
                .ToList();

            if (string.IsNullOrWhiteSpace(imageFileName))
            {
                if (orderedImages.Count == 0)
                {
                    return;
                }

                db.PostImages.RemoveRange(orderedImages);
                post.Images.Clear();
                return;
            }

            await using var imageStream = CreateSeedImageStream();
            var imageUrl = await imageService.UploadAsync(imageStream, imageFileName);

            var primaryImage = orderedImages.FirstOrDefault();
            if (primaryImage is null)
            {
                post.Images.Add(new PostImage
                {
                    Post = post,
                    ImageUrl = imageUrl,
                    PreviewUrl = imageUrl,
                    ThumbnailUrl = imageUrl
                });
                return;
            }

            primaryImage.ImageUrl = imageUrl;
            primaryImage.PreviewUrl = imageUrl;
            primaryImage.ThumbnailUrl = imageUrl;

            if (orderedImages.Count <= 1)
            {
                return;
            }

            var extraImages = orderedImages.Skip(1).ToList();
            db.PostImages.RemoveRange(extraImages);
            foreach (var extraImage in extraImages)
            {
                post.Images.Remove(extraImage);
            }
        }

        private static MemoryStream CreateSeedImageStream()
        {
            const int width = 480;
            const int height = 320;

            using var image = new Image<Rgba32>(width, height);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var inCenterPanel =
                        x >= width / 6 && x <= width * 5 / 6
                        && y >= height / 5 && y <= height * 4 / 5;
                    var checker = ((x / 24) + (y / 24)) % 2 == 0;

                    image[x, y] = inCenterPanel
                        ? checker
                            ? new Rgba32(230, 179, 82)
                            : new Rgba32(194, 120, 47)
                        : checker
                            ? new Rgba32(46, 84, 158)
                            : new Rgba32(75, 120, 204);
                }
            }

            var stream = new MemoryStream();
            image.SaveAsPng(stream);
            stream.Position = 0;
            return stream;
        }

        private sealed record SeedPostDefinition(
            string UserName,
            string Title,
            string Content,
            IReadOnlyList<string> Tags,
            string? ImageFileName);
    }
}
