using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class SearchServiceTests
{
    [Fact]
    public async Task SearchPostsAsync_PrioritizesExactTagMatchesAndSkipsDeletedPosts()
    {
        await using var context = AppDbContextFactory.Create();
        var author = CreateUserWithProfile("author-1", "author-one", "Author One");
        var glazingTag = CreateTag(1, "Glazing", "glazing");
        var accentTag = CreateTag(2, "Accent", "accent");

        var exactTagPost = CreatePost(1, author, "Dragon notes", "Working through sharp reflections.", createdUtc: new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));
        var titleMatchPost = CreatePost(2, author, "Glazing workflow", "Layer blends for armor plates.", createdUtc: new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc));
        var deletedPost = CreatePost(3, author, "Deleted glazing log", "Should not appear.", createdUtc: new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc), isDeleted: true);

        LinkTag(exactTagPost, glazingTag);
        LinkTag(titleMatchPost, accentTag);
        LinkTag(deletedPost, glazingTag);

        context.Users.Add(author);
        context.Profiles.Add(author.Profile!);
        context.Tags.AddRange(glazingTag, accentTag);
        context.Posts.AddRange(exactTagPost, titleMatchPost, deletedPost);
        await context.SaveChangesAsync();

        var service = new SearchService(context);

        var result = await service.SearchPostsAsync("glazing", tagSlug: null, page: 1, pageSize: 10);
        var items = result.Items.ToList();

        items.Select(post => post.Id).Should().Equal(exactTagPost.Id, titleMatchPost.Id);
        items.Should().OnlyContain(post => !post.IsDeleted);
        items[0].Tags.Should().ContainSingle(tag => tag.Slug == "glazing");
    }

    [Fact]
    public async Task SearchUsersAsync_OrdersStartsWithMatchesAheadOfContainsMatches()
    {
        await using var context = AppDbContextFactory.Create();
        var startsWithUser = CreateUserWithProfile("user-1", "glowmaster", "Glow Master", bio: "Airbrush glow recipes");
        var containsUser = CreateUserWithProfile("user-2", "studio-user", "Studio Glow", bio: "Display cabinet log");
        var bioOnlyUser = CreateUserWithProfile("user-3", "weatherer", "Panel Liner", bio: "Glow pigments for engines");

        context.Users.AddRange(startsWithUser, containsUser, bioOnlyUser);
        context.Profiles.AddRange(startsWithUser.Profile!, containsUser.Profile!, bioOnlyUser.Profile!);
        await context.SaveChangesAsync();

        var service = new SearchService(context);

        var result = await service.SearchUsersAsync("glow", page: 1, pageSize: 10);

        result.Items.Select(user => user.DisplayName).Should().Equal("Glow Master", "Studio Glow", "Panel Liner");
        result.Items.Select(user => user.UserName).Should().Contain("glowmaster");
    }

    [Fact]
    public async Task SearchTagsAsync_ReturnsExactMatchBeforeBroaderMatches()
    {
        await using var context = AppDbContextFactory.Create();
        var author = CreateUserWithProfile("author-1", "author-one", "Author One");
        var exactTag = CreateTag(1, "Weathering", "weathering");
        var startsWithTag = CreateTag(2, "Weathering Oils", "weathering-oils");
        var containsTag = CreateTag(3, "Battle Weathering", "battle-weathering");

        var basePost = CreatePost(1, author, "Tank", "Rust streaks");
        var extraPostA = CreatePost(2, author, "Walker", "Oil wash");
        var extraPostB = CreatePost(3, author, "Knight", "Mud and grime");

        LinkTag(basePost, exactTag);
        LinkTag(extraPostA, startsWithTag);
        LinkTag(extraPostB, startsWithTag);
        LinkTag(extraPostB, containsTag);

        context.Users.Add(author);
        context.Profiles.Add(author.Profile!);
        context.Tags.AddRange(exactTag, startsWithTag, containsTag);
        context.Posts.AddRange(basePost, extraPostA, extraPostB);
        await context.SaveChangesAsync();

        var service = new SearchService(context);

        var result = await service.SearchTagsAsync("weathering", page: 1, pageSize: 10);
        var items = result.Items.ToList();

        items.Select(tag => tag.Name).Should().Equal("Weathering", "Weathering Oils", "Battle Weathering");
        items[0].PostCount.Should().Be(1);
        items[1].PostCount.Should().Be(2);
    }

    [Fact]
    public async Task GetOverviewAsync_WhenQueryIsTooShort_ReturnsEmptyResults()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new SearchService(context);

        var result = await service.GetOverviewAsync("g");

        result.Posts.Should().BeEmpty();
        result.Users.Should().BeEmpty();
        result.Tags.Should().BeEmpty();
    }

    private static ApplicationUser CreateUserWithProfile(string id, string userName, string displayName, string? bio = null)
    {
        var user = TestData.CreateUser(id, userName);
        var profile = TestData.CreateProfile(id, displayName, bio ?? string.Empty);
        profile.User = user;
        user.Profile = profile;
        return user;
    }

    private static Tag CreateTag(int id, string name, string slug)
        => new()
        {
            Id = id,
            DisplayName = name,
            NormalizedName = name.ToLowerInvariant(),
            Slug = slug,
            CreatedUtc = DateTime.UtcNow
        };

    private static Post CreatePost(int id, ApplicationUser author, string title, string content, DateTime? createdUtc = null, bool isDeleted = false)
    {
        var post = TestData.CreatePost(id, author.Id, isDeleted: isDeleted);
        post.Title = title;
        post.Content = content;
        post.CreatedBy = author;
        post.CreatedUtc = createdUtc ?? DateTime.UtcNow;
        post.UpdatedUtc = post.CreatedUtc;
        return post;
    }

    private static void LinkTag(Post post, Tag tag)
    {
        var link = new PostTag
        {
            PostId = post.Id,
            Post = post,
            TagId = tag.Id,
            Tag = tag
        };

        post.PostTags.Add(link);
        tag.PostTags.Add(link);
    }
}
