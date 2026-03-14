using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;
using System.Linq;

namespace MiniPainterHub.Server.Tests.Data;

public class DataSeederTests
{
    [Fact]
    public async Task SeedAsync_WhenCalledTwice_IsIdempotent()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();

        await DataSeeder.SeedAsync(factory.Services);
        await DataSeeder.SeedAsync(factory.Services);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Users.Should().HaveCount(2);
        db.Roles.Should().HaveCount(3);
        db.Posts.Should().HaveCount(2);
        db.PostImages.Should().HaveCount(2);
        db.Tags.Should().HaveCount(7);
        db.PostTags.Should().HaveCount(8);
        (await roles.RoleExistsAsync("Admin")).Should().BeTrue();
        (await roles.RoleExistsAsync("User")).Should().BeTrue();
        (await roles.RoleExistsAsync("Moderator")).Should().BeTrue();

        var admin = await users.FindByEmailAsync("admin@local");
        admin.Should().NotBeNull();
        (await users.IsInRoleAsync(admin!, "Admin")).Should().BeTrue();
        (await users.IsInRoleAsync(admin!, "User")).Should().BeTrue();

        var normalUser = await users.FindByEmailAsync("user@local");
        normalUser.Should().NotBeNull();
        (await users.IsInRoleAsync(normalUser!, "User")).Should().BeTrue();

        var imageBackedPosts = await db.Posts
            .Include(post => post.Images)
            .Include(post => post.PostTags)
            .ThenInclude(postTag => postTag.Tag)
            .Where(post => post.Images.Any())
            .ToListAsync();

        imageBackedPosts.Should().HaveCount(2);
        imageBackedPosts.Should().OnlyContain(post => post.PostTags.Count > 0);
        imageBackedPosts.SelectMany(post => post.Images)
            .Should()
            .OnlyContain(image => image.ImageUrl.StartsWith("/uploads/images/", StringComparison.Ordinal));

        var glazingPost = imageBackedPosts.Single(post => post.Title == "Seeded: glazing check");
        glazingPost.PostTags
            .Select(postTag => postTag.Tag.DisplayName)
            .Should()
            .BeEquivalentTo("glazing", "nmm", "display", "seeded");
    }

    [Fact]
    public async Task SeedAsync_WhenPostsAlreadyExist_DoesNotAddBaselineSeedPosts()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Posts.Add(new MiniPainterHub.Server.Entities.Post
            {
                Id = 777,
                CreatedById = "external-user",
                Title = "Existing migration content",
                Content = "Do not overwrite rich datasets.",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        await DataSeeder.SeedAsync(factory.Services);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Posts.Should().ContainSingle(post => post.Title == "Existing migration content");
            db.Posts.Should().NotContain(post => post.Title == "Seeded: glazing check");
            db.Posts.Should().NotContain(post => post.Title == "Seeded: weathering notes");
            db.PostImages.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task SeedAdminAsync_WhenDisabled_DoesNothing()
    {
        await using var app = await BuildSeedAdminApp(new Dictionary<string, string?>
        {
            ["SeedAdmin:Enabled"] = "false",
            ["SeedAdmin:Email"] = "admin@example.test",
            ["SeedAdmin:Password"] = "Admin123!",
            ["SeedAdmin:Role"] = "Admin"
        });

        await DataSeeder.SeedAdminAsync(app);

        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        (await users.FindByEmailAsync("admin@example.test")).Should().BeNull();
        (await roles.RoleExistsAsync("Admin")).Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_WhenAdminAlreadyExistsWithDifferentPassword_ResetsPasswordAndEnsuresRoles()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var create = await users.CreateAsync(new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@local",
                EmailConfirmed = true
            }, "OldPass123!");

            create.Succeeded.Should().BeTrue();
        }

        await DataSeeder.SeedAsync(factory.Services);

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await users.FindByNameAsync("admin");
            admin.Should().NotBeNull();
            (await users.CheckPasswordAsync(admin!, "P@ssw0rd!")).Should().BeTrue();
            (await users.IsInRoleAsync(admin!, "Admin")).Should().BeTrue();
            (await users.IsInRoleAsync(admin!, "User")).Should().BeTrue();
        }
    }

    private static async Task<WebApplication> BuildSeedAdminApp(IDictionary<string, string?> configuration)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
            ApplicationName = typeof(DataSeederTests).Assembly.FullName
        });

        builder.Configuration.AddInMemoryCollection(configuration);

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"seed-admin-tests-{Guid.NewGuid():N}"));

        builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        var app = builder.Build();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        return app;
    }
}
