using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Data;

public class DevelopmentContentSeederTests
{
    [Fact]
    public async Task ResetAndSeedAsync_CreatesTenUsersProfilesPostsAndAvatars()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "MiniPainterHub.SeedTests", Guid.NewGuid().ToString("N"));
        var imageRoot = Path.Combine(testRoot, "storage");
        var avatarSource = Path.Combine(testRoot, "avatars");
        Directory.CreateDirectory(imageRoot);

        try
        {
            await CreateAvatarSourceAsync(avatarSource, ".png");

            using var factory = new IntegrationTestApplicationFactory(new Dictionary<string, string?>
            {
                ["ImageStorage:LocalPath"] = imageRoot,
                ["ImageStorage:RequestPath"] = "/uploads/images"
            });

            using var scope = factory.Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentContentSeeder>();
            var result = await seeder.ResetAndSeedAsync(avatarSource);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            result.UsersCreated.Should().Be(10);
            result.PostsCreated.Should().Be(20);
            result.AvatarsImported.Should().Be(10);
            db.Users.Should().HaveCount(10);
            db.Profiles.Should().HaveCount(10);
            db.Posts.Should().HaveCount(20);

            db.Users.Select(u => u.UserName).Should().Contain(new[] { "admin", "user", "studiomod" });
            db.Users.OfType<ApplicationUser>().All(u => !string.IsNullOrWhiteSpace(u.AvatarUrl)).Should().BeTrue();
            Directory.GetFiles(imageRoot).Should().HaveCount(10);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GenerateAvatarsOnlyAsync_ImportsAvatarsWithoutCreatingData_AndCanBeRunRepeatedly()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "MiniPainterHub.SeedTests", Guid.NewGuid().ToString("N"));
        var imageRoot = Path.Combine(testRoot, "storage");
        var avatarSource = Path.Combine(testRoot, "avatars");
        Directory.CreateDirectory(imageRoot);

        try
        {
            await CreateAvatarSourceAsync(avatarSource, ".png");

            using var factory = new IntegrationTestApplicationFactory(new Dictionary<string, string?>
            {
                ["ImageStorage:LocalPath"] = imageRoot,
                ["ImageStorage:RequestPath"] = "/uploads/images"
            });

            using var scope = factory.Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentContentSeeder>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var initialUserCount = db.Users.Count();
            var initialProfileCount = db.Profiles.Count();
            var initialPostCount = db.Posts.Count();

            var firstResult = await seeder.GenerateAvatarsOnlyAsync(avatarSource);
            var secondResult = await seeder.GenerateAvatarsOnlyAsync(avatarSource);
            var existingSeedUsers = db.Users
                .OfType<ApplicationUser>()
                .AsEnumerable()
                .Count(user => firstResult.Avatars.Any(avatar => string.Equals(avatar.UserName, user.UserName, StringComparison.OrdinalIgnoreCase)));

            firstResult.AvatarsImported.Should().Be(10);
            firstResult.ExistingUsersUpdated.Should().Be(existingSeedUsers);
            secondResult.AvatarsImported.Should().Be(10);
            secondResult.ExistingUsersUpdated.Should().Be(existingSeedUsers);
            db.Users.Should().HaveCount(initialUserCount);
            db.Profiles.Should().HaveCount(initialProfileCount);
            db.Posts.Should().HaveCount(initialPostCount);
            Directory.GetFiles(imageRoot).Should().HaveCount(10);
            db.Users.OfType<ApplicationUser>()
                .AsEnumerable()
                .Where(user => firstResult.Avatars.Any(avatar => string.Equals(avatar.UserName, user.UserName, StringComparison.OrdinalIgnoreCase)))
                .Select(user => user.AvatarUrl)
                .Should()
                .OnlyContain(url => !string.IsNullOrWhiteSpace(url));
            secondResult.Avatars.Select(avatar => avatar.AvatarUrl)
                .Should()
                .Equal(firstResult.Avatars.Select(avatar => avatar.AvatarUrl));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GenerateAvatarsOnlyAsync_RefreshesExistingSeedUserAvatars_WithoutResettingOtherData()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "MiniPainterHub.SeedTests", Guid.NewGuid().ToString("N"));
        var imageRoot = Path.Combine(testRoot, "storage");
        var avatarSourcePng = Path.Combine(testRoot, "avatars-png");
        var avatarSourceJpg = Path.Combine(testRoot, "avatars-jpg");
        Directory.CreateDirectory(imageRoot);

        try
        {
            await CreateAvatarSourceAsync(avatarSourcePng, ".png");
            await CreateAvatarSourceAsync(avatarSourceJpg, ".jpg");

            using var factory = new IntegrationTestApplicationFactory(new Dictionary<string, string?>
            {
                ["ImageStorage:LocalPath"] = imageRoot,
                ["ImageStorage:RequestPath"] = "/uploads/images"
            });

            string adminUserId;
            using (var seedScope = factory.Services.CreateScope())
            {
                var seeder = seedScope.ServiceProvider.GetRequiredService<DevelopmentContentSeeder>();
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

                await seeder.ResetAndSeedAsync(avatarSourcePng);

                var adminUser = db.Users.OfType<ApplicationUser>().Single(user => user.UserName == "admin");
                adminUser.AvatarUrl.Should().EndWith(".png");

                adminUserId = adminUser.Id;
                db.Posts.Add(new Post
                {
                    Id = 9999,
                    Title = "Persistent post",
                    Content = "Should survive avatar refresh.",
                    CreatedById = adminUserId,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow,
                    IsDeleted = false
                });
                await db.SaveChangesAsync();

                var result = await seeder.GenerateAvatarsOnlyAsync(avatarSourceJpg);
                result.AvatarsImported.Should().Be(10);
                result.ExistingUsersUpdated.Should().Be(10);
            }

            using var verifyScope = factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refreshedAdmin = verifyDb.Users.OfType<ApplicationUser>().Single(user => user.Id == adminUserId);
            var refreshedProfile = verifyDb.Profiles.Single(profile => profile.UserId == adminUserId);

            verifyDb.Users.Should().HaveCount(10);
            verifyDb.Posts.Should().HaveCount(21);
            refreshedAdmin.AvatarUrl.Should().EndWith(".jpg");
            refreshedProfile.AvatarUrl.Should().EndWith(".jpg");
            Directory.GetFiles(imageRoot).Should().HaveCount(10);
            Directory.GetFiles(imageRoot)
                .Should()
                .OnlyContain(file => string.Equals(Path.GetExtension(file), ".jpg", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static async Task CreateAvatarSourceAsync(string avatarSource, string extension)
    {
        Directory.CreateDirectory(avatarSource);

        for (var i = 1; i <= 10; i++)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(avatarSource, $"avatar-{i:00}{extension}"),
                CreateAvatarBytes(extension, i));
        }
    }

    private static byte[] CreateAvatarBytes(string extension, int index) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => new byte[] { 255, 216, 255, (byte)index },
        ".webp" => new byte[] { 82, 73, 70, 70, (byte)index },
        _ => new byte[] { 137, 80, 78, 71, (byte)index }
    };
}
