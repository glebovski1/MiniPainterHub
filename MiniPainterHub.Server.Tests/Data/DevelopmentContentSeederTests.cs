using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
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
    public async Task ResetAndSeedAsync_CreatesTenUsersProfilesPostsCommentsAndAvatars()
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
            result.CommentsCreated.Should().Be(16);
            result.AvatarsImported.Should().Be(10);
            result.PostImagesImported.Should().Be(0);
            db.Users.Should().HaveCount(10);
            db.Profiles.Should().HaveCount(10);
            db.Posts.Should().HaveCount(20);
            db.Comments.Should().HaveCount(16);
            db.Follows.Should().HaveCount(12);
            db.Conversations.Should().HaveCount(4);
            db.DirectMessages.Should().HaveCount(12);

            db.Users.Select(u => u.UserName).Should().Contain(new[] { "admin", "user", "studiomod" });
            db.Users.OfType<ApplicationUser>().All(u => !string.IsNullOrWhiteSpace(u.AvatarUrl)).Should().BeTrue();
            Directory.GetFiles(imageRoot, "seed-avatar-*").Should().HaveCount(10);
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
    public async Task ResetAndSeedAsync_AssignsTagsToEverySeededPost_ForTagDiscoverySurfaces()
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

            await seeder.ResetAndSeedAsync(avatarSource);

            var posts = db.Posts
                .Include(post => post.PostTags)
                .ThenInclude(postTag => postTag.Tag)
                .ToList();
            var weatheringTag = db.Tags
                .Include(tag => tag.PostTags)
                .Single(tag => tag.Slug == "weathering");
            var snowPost = posts.Single(post => post.Title == "Snow basing experiment");

            db.Tags.Should().NotBeEmpty();
            db.PostTags.Should().NotBeEmpty();
            posts.Should().HaveCount(20);
            posts.Should().OnlyContain(post => post.PostTags.Count > 0);
            weatheringTag.PostTags.Count.Should().BeGreaterThan(1);
            snowPost.PostTags
                .Select(postTag => postTag.Tag.DisplayName)
                .Should()
                .BeEquivalentTo("basing", "snow", "winter");
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
    public async Task ResetAndSeedAsync_WithPostImageSource_AttachesOneSeedImageToEachPost_ReusingFilesAsNeeded()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "MiniPainterHub.SeedTests", Guid.NewGuid().ToString("N"));
        var imageRoot = Path.Combine(testRoot, "storage");
        var avatarSource = Path.Combine(testRoot, "avatars");
        var postImageSource = Path.Combine(testRoot, "post-images");
        Directory.CreateDirectory(imageRoot);

        try
        {
            await CreateAvatarSourceAsync(avatarSource, ".png");
            await CreatePostImageSourceAsync(postImageSource, 5, ".jpg");

            using var factory = new IntegrationTestApplicationFactory(new Dictionary<string, string?>
            {
                ["ImageStorage:LocalPath"] = imageRoot,
                ["ImageStorage:RequestPath"] = "/uploads/images"
            });

            using var scope = factory.Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentContentSeeder>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var result = await seeder.ResetAndSeedAsync(avatarSource, postImageSource);

            result.UsersCreated.Should().Be(10);
            result.PostsCreated.Should().Be(20);
            result.AvatarsImported.Should().Be(10);
            result.PostImagesImported.Should().Be(20);

            db.Posts.Include(post => post.Images).Should().OnlyContain(post => post.Images.Count == 1);
            db.Posts
                .Include(post => post.Images)
                .Include(post => post.PostTags)
                .Should()
                .OnlyContain(post => post.Images.Count == 1 && post.PostTags.Count > 0);
            db.PostImages.Should().HaveCount(20);
            db.PostImages.Select(image => image.ImageUrl).Should().OnlyContain(url => !string.IsNullOrWhiteSpace(url));
            db.PostImages.Select(image => image.ThumbnailUrl).Should().OnlyContain(url => !string.IsNullOrWhiteSpace(url));
            db.PostImages.Select(image => image.PreviewUrl).Should().OnlyContain(url => !string.IsNullOrWhiteSpace(url));
            Directory.GetFiles(imageRoot).Should().HaveCount(30);
            Directory.GetFiles(imageRoot, "seed-post-*").Should().HaveCount(20);
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
    public async Task ResetAndSeedAsync_SeedsFollowAndConversationData_ForMessagingAndFollowerTesting()
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
            var follows = scope.ServiceProvider.GetRequiredService<IFollowService>();
            var conversations = scope.ServiceProvider.GetRequiredService<IConversationService>();

            await seeder.ResetAndSeedAsync(avatarSource);

            var adminId = db.Users.OfType<ApplicationUser>().Single(user => user.UserName == "admin").Id;
            var userId = db.Users.OfType<ApplicationUser>().Single(user => user.UserName == "user").Id;
            var studiomodId = db.Users.OfType<ApplicationUser>().Single(user => user.UserName == "studiomod").Id;

            var adminFollowing = await follows.GetFollowingAsync(adminId);
            var adminFollowers = await follows.GetFollowersAsync(adminId);
            var adminConversations = await conversations.GetConversationsAsync(adminId);
            var adminToUserConversation = adminConversations.Single(conversation => conversation.OtherUser.UserId == userId);
            var adminMessages = await conversations.GetMessagesAsync(adminId, adminToUserConversation.Id, beforeMessageId: null, pageSize: 20);
            var studiomodConversations = await conversations.GetConversationsAsync(studiomodId);

            adminFollowing.Select(profile => profile.UserId).Should().Contain(new[] { userId, studiomodId });
            adminFollowers.Select(profile => profile.UserId).Should().Contain(new[] { userId, studiomodId });
            adminConversations.Should().HaveCount(2);
            adminConversations.Should().Contain(conversation => conversation.OtherUser.UserId == studiomodId);
            adminConversations.Should().Contain(conversation => conversation.UnreadCount > 0);
            adminToUserConversation.UnreadCount.Should().Be(1);
            adminMessages.Items.Should().HaveCount(4);
            adminMessages.Items.Last().Body.Should().Contain("badge");
            studiomodConversations.Should().Contain(conversation => conversation.OtherUser.UserId == adminId);
            studiomodConversations.Should().Contain(conversation => conversation.OtherUser.UserId == userId);
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
    public async Task ResetAndSeedAsync_SeedsCrossUserComments_OnOtherUsersPosts()
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
            var comments = scope.ServiceProvider.GetRequiredService<ICommentService>();

            await seeder.ResetAndSeedAsync(avatarSource);

            var seededComments = db.Comments
                .Include(comment => comment.Author)
                .Include(comment => comment.Post)
                .ToList();
            var rustedSentinelPostId = db.Posts.Single(post => post.Title == "WIP: rusted sentinel captain").Id;
            var rustedSentinelComments = await comments.GetByPostIdAsync(rustedSentinelPostId, page: 1, pageSize: 10);

            seededComments.Should().HaveCount(16);
            seededComments.Should().OnlyContain(comment => comment.AuthorId != comment.Post.CreatedById);
            seededComments
                .Select(comment => comment.Author.UserName)
                .Where(userName => !string.IsNullOrWhiteSpace(userName))
                .Distinct()
                .Count()
                .Should()
                .BeGreaterOrEqualTo(8);
            rustedSentinelComments.Items.Should().ContainSingle(comment =>
                comment.AuthorName == "inkandiron"
                && comment.Content.Contains("copper undercoat", StringComparison.Ordinal));
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
            Directory.GetFiles(imageRoot, "seed-avatar-*").Should().HaveCount(10);
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

    private static async Task CreatePostImageSourceAsync(string postImageSource, int fileCount, string extension)
    {
        Directory.CreateDirectory(postImageSource);

        for (var i = 1; i <= fileCount; i++)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(postImageSource, $"post-image-{i:00}{extension}"),
                CreateAvatarBytes(extension, i + 32));
        }
    }

    private static byte[] CreateAvatarBytes(string extension, int index) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => new byte[] { 255, 216, 255, (byte)index },
        ".webp" => new byte[] { 82, 73, 70, 70, (byte)index },
        _ => new byte[] { 137, 80, 78, 71, (byte)index }
    };
}
