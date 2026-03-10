using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using System;
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
        var imageRoot = Path.Combine(Path.GetTempPath(), "MiniPainterHub.SeedTests", Guid.NewGuid().ToString("N"), "storage");
        var avatarSource = Path.Combine(Path.GetTempPath(), "MiniPainterHub.SeedTests", Guid.NewGuid().ToString("N"), "avatars");
        Directory.CreateDirectory(imageRoot);
        Directory.CreateDirectory(avatarSource);

        try
        {
            for (var i = 1; i <= 10; i++)
            {
                await File.WriteAllBytesAsync(
                    Path.Combine(avatarSource, $"avatar-{i:00}.png"),
                    new byte[] { 137, 80, 78, 71, (byte)i });
            }

            using var factory = new IntegrationTestApplicationFactory(new System.Collections.Generic.Dictionary<string, string?>
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
            if (Directory.Exists(imageRoot))
            {
                Directory.Delete(Path.GetDirectoryName(imageRoot)!, recursive: true);
            }

            if (Directory.Exists(avatarSource))
            {
                Directory.Delete(Path.GetDirectoryName(avatarSource)!, recursive: true);
            }
        }
    }
}
