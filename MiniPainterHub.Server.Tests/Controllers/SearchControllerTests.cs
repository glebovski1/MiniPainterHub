using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class SearchControllerTests
{
    [Fact]
    public async Task SearchPosts_WhenQueryMatchesExactTag_RanksTaggedPostFirst()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "author-1", "author-1");
        await SeedPostAsync(factory, 10, "author-1", "Layering basics", "Smooth blends", isDeleted: false, tags: new[] { "nmm" });
        await SeedPostAsync(factory, 11, "author-1", "nmm showcase", "Title match only");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search/posts?q=nmm&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<PostSummaryDto>>();
        body.Should().NotBeNull();
        body!.Items.Select(item => item.Id).Should().ContainInOrder(10, 11);
        body.Items.First().Tags.Select(tag => tag.Name).Should().Contain("nmm");
    }

    [Fact]
    public async Task SearchUsers_WhenQueryMatchesDisplayName_ReturnsUsers()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "artist-1", "artist1", displayName: "Crystal Painter");
        await SeedUserAsync(factory, "artist-2", "artist2", displayName: "Layer Master");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search/users?q=crystal&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle();
        body.Items.Single().DisplayName.Should().Be("Crystal Painter");
    }

    [Fact]
    public async Task SearchTags_WhenPostsAreHidden_CountsOnlyVisiblePosts()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "author-1", "author-1");
        await SeedPostAsync(factory, 21, "author-1", "Visible", "Visible post", isDeleted: false, tags: new[] { "glazing" });
        await SeedPostAsync(factory, 22, "author-1", "Hidden", "Hidden post", isDeleted: true, tags: new[] { "glazing" });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search/tags?q=gl&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<SearchTagResultDto>>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle();
        body.Items.Single().Name.Should().Be("glazing");
        body.Items.Single().PostCount.Should().Be(1);
    }

    private static Task SeedUserAsync(
        IntegrationTestApplicationFactory factory,
        string userId,
        string userName,
        string? displayName = null)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Users.AddAsync(new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                Email = $"{userName}@example.test",
                Profile = new Profile
                {
                    UserId = userId,
                    DisplayName = displayName ?? userName
                }
            });

            await db.SaveChangesAsync();
        });
    }

    private static Task SeedPostAsync(
        IntegrationTestApplicationFactory factory,
        int postId,
        string userId,
        string title,
        string content,
        bool isDeleted = false,
        string[]? tags = null)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            var post = new Post
            {
                Id = postId,
                Title = title,
                Content = content,
                CreatedById = userId,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                IsDeleted = isDeleted
            };

            await db.Posts.AddAsync(post);
            await db.SaveChangesAsync();

            if (tags is null)
            {
                return;
            }

            foreach (var tagName in tags)
            {
                var trimmed = tagName.Trim();
                var normalized = trimmed.ToLowerInvariant();
                var tag = await db.Tags.FirstOrDefaultAsync(t => t.NormalizedName == normalized);
                if (tag is null)
                {
                    tag = new Tag
                    {
                        DisplayName = trimmed,
                        NormalizedName = normalized,
                        Slug = normalized.Replace(' ', '-'),
                        CreatedUtc = DateTime.UtcNow
                    };
                    await db.Tags.AddAsync(tag);
                    await db.SaveChangesAsync();
                }

                await db.PostTags.AddAsync(new PostTag { PostId = postId, TagId = tag.Id });
            }

            await db.SaveChangesAsync();
        });
    }
}
