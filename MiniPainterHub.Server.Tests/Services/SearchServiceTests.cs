using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
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
        exactTagPost.MiniatureName = "Sun Dragon";
        exactTagPost.Techniques = "Glazing, edge highlights";
        exactTagPost.Difficulty = "Intermediate";
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
        items[0].MiniatureName.Should().Be("Sun Dragon");
        items[0].Techniques.Should().Be("Glazing, edge highlights");
        items[0].Difficulty.Should().Be("Intermediate");
    }

    [Fact]
    public async Task SearchPostsAsync_EscapesLikeWildcards()
    {
        await using var context = AppDbContextFactory.Create();
        var author = CreateUserWithProfile("author-1", "author-one", "Author One");
        var literalPost = CreatePost(1, author, "G_ marker", "Literal underscore search token.");
        var wildcardCandidate = CreatePost(2, author, "Ga marker", "This should not match an escaped underscore query.");

        context.Users.Add(author);
        context.Profiles.Add(author.Profile!);
        context.Posts.AddRange(literalPost, wildcardCandidate);
        await context.SaveChangesAsync();

        var service = new SearchService(context);

        var result = await service.SearchPostsAsync("g_", tagSlug: null, page: 1, pageSize: 10);

        result.Items.Select(post => post.Id).Should().Equal(literalPost.Id);
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
    public async Task SearchProjectsAsync_RanksTitleMatchesAndExcludesNonPublicProjects()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = CreateUserWithProfile("project-owner", "project-owner", "Project Owner");
        var titlePost = CreatePost(31, owner, "Title project progress", "Visible");
        titlePost.Images.Add(new PostImage
        {
            Id = 301,
            ImageUrl = "/uploads/images/winter.jpg",
            ThumbnailUrl = "/uploads/images/winter-thumb.jpg"
        });
        var descriptionPost = CreatePost(32, owner, "Description project progress", "Visible");
        var hiddenPost = CreatePost(33, owner, "Hidden project progress", "Visible");
        var archivedPost = CreatePost(34, owner, "Archived project progress", "Visible");
        var titleProject = CreateProject(101, owner, titlePost, "Winter Cohort", "A completed army.");
        titleProject.CoverPost = titlePost;
        titleProject.CoverPostId = titlePost.Id;
        titleProject.UpdatedUtc = DateTime.UtcNow.AddDays(-3);
        var descriptionProject = CreateProject(102, owner, descriptionPost, "Campaign Force", "Cold winter basing experiments.");
        descriptionProject.UpdatedUtc = DateTime.UtcNow;
        var hiddenProject = CreateProject(103, owner, hiddenPost, "Winter Hidden", "Hidden");
        hiddenProject.IsHidden = true;
        var archivedProject = CreateProject(104, owner, archivedPost, "Winter Archived", "Archived");
        archivedProject.ArchivedUtc = DateTime.UtcNow;
        var emptyProject = CreateProject(105, owner, post: null, "Winter Empty", "No visible progress");

        context.Users.Add(owner);
        context.Profiles.Add(owner.Profile!);
        context.Posts.AddRange(titlePost, descriptionPost, hiddenPost, archivedPost);
        context.HobbyProjects.AddRange(titleProject, descriptionProject, hiddenProject, archivedProject, emptyProject);
        await context.SaveChangesAsync();
        var service = new SearchService(context);

        var result = await service.SearchProjectsAsync("winter", page: 1, pageSize: 10);
        var items = result.Items.ToList();

        items.Select(project => project.Id).Should().Equal(titleProject.Id, descriptionProject.Id);
        items[0].CoverImageUrl.Should().Be("/uploads/images/winter.jpg");
        items[0].CoverThumbnailUrl.Should().Be("/uploads/images/winter-thumb.jpg");
        items.Should().OnlyContain(project => project.IsPublic && !project.IsHidden && !project.IsArchived);
    }

    [Fact]
    public async Task GetOverviewAsync_WhenProjectMatches_IncludesPublicProject()
    {
        await using var context = AppDbContextFactory.Create();
        var owner = CreateUserWithProfile("overview-project-owner", "overview-project-owner", "Project Owner");
        var post = CreatePost(35, owner, "Moonlit progress", "Visible progress");
        var project = CreateProject(106, owner, post, "Moonlit Warband", "A cool-toned showcase project.");
        context.Users.Add(owner);
        context.Profiles.Add(owner.Profile!);
        context.Posts.Add(post);
        context.HobbyProjects.Add(project);
        await context.SaveChangesAsync();
        var service = new SearchService(context);

        var overview = await service.GetOverviewAsync("moonlit");

        overview.Projects.Should().ContainSingle(item => item.Id == project.Id);
    }

    [Fact]
    public async Task SearchPostsAsync_WithMemoryCache_ExposesProjectReferenceOnlyWhileMembershipIsPublic()
    {
        await using var context = AppDbContextFactory.Create();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var owner = CreateUserWithProfile("post-project-owner", "post-project-owner", "Post Project Owner");
        var post = CreatePost(41, owner, "Moonlit armor progress", "Moonlit glazing layers.");
        var project = CreateProject(141, owner, post, "Moonlit Army", "A public project.");
        context.Users.Add(owner);
        context.Profiles.Add(owner.Profile!);
        context.Posts.Add(post);
        context.HobbyProjects.Add(project);
        await context.SaveChangesAsync();
        var service = new SearchService(context, cache);

        var initial = await service.SearchPostsAsync("moonlit", null, 1, 10);
        initial.Items.Should().ContainSingle();
        initial.Items.Single().Project.Should().BeEquivalentTo(new HobbyProjectReferenceDto
        {
            Id = project.Id,
            Title = project.Title,
            Status = project.Status,
            IsPublic = true
        });

        var trackedProject = await context.HobbyProjects.SingleAsync(item => item.Id == project.Id);
        trackedProject.IsHidden = true;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var hidden = await service.SearchPostsAsync("moonlit", null, 1, 10);
        hidden.Items.Should().ContainSingle().Which.Project.Should().BeNull();

        trackedProject = await context.HobbyProjects.SingleAsync(item => item.Id == project.Id);
        trackedProject.IsHidden = false;
        trackedProject.ArchivedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var archived = await service.SearchPostsAsync("moonlit", null, 1, 10);
        archived.Items.Should().ContainSingle().Which.Project.Should().BeNull();
    }

    [Fact]
    public async Task ProjectSearchAndOverview_WithMemoryCache_ImmediatelyReflectHideArchiveAndFinalUnlink()
    {
        await using var context = AppDbContextFactory.Create();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var owner = CreateUserWithProfile("cache-project-owner", "cache-project-owner", "Cache Project Owner");
        var post = CreatePost(42, owner, "Aurora project progress", "Aurora color study.");
        var project = CreateProject(142, owner, post, "Aurora Warband", "Aurora display project.");
        context.Users.Add(owner);
        context.Profiles.Add(owner.Profile!);
        context.Posts.Add(post);
        context.HobbyProjects.Add(project);
        await context.SaveChangesAsync();
        var service = new SearchService(context, cache);

        async Task AssertVisibleAsync(bool expectedVisible)
        {
            var projects = await service.SearchProjectsAsync("aurora", 1, 10);
            var overview = await service.GetOverviewAsync("aurora");
            projects.Items.Any(item => item.Id == project.Id).Should().Be(expectedVisible);
            overview.Projects.Any(item => item.Id == project.Id).Should().Be(expectedVisible);
            var postResult = overview.Posts.Should().ContainSingle(item => item.Id == post.Id).Which;
            if (expectedVisible)
            {
                postResult.Project.Should().NotBeNull();
                postResult.Project!.Id.Should().Be(project.Id);
                postResult.Project.IsPublic.Should().BeTrue();
            }
            else
            {
                postResult.Project.Should().BeNull();
            }
        }

        await AssertVisibleAsync(true); // Prime project, overview, and post keys.

        var trackedProject = await context.HobbyProjects.SingleAsync(item => item.Id == project.Id);
        trackedProject.IsHidden = true;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await AssertVisibleAsync(false);

        trackedProject = await context.HobbyProjects.SingleAsync(item => item.Id == project.Id);
        trackedProject.IsHidden = false;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await AssertVisibleAsync(true);

        trackedProject = await context.HobbyProjects.SingleAsync(item => item.Id == project.Id);
        trackedProject.ArchivedUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await AssertVisibleAsync(false);

        trackedProject = await context.HobbyProjects.SingleAsync(item => item.Id == project.Id);
        trackedProject.ArchivedUtc = null;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await AssertVisibleAsync(true);

        var entry = await context.HobbyProjectEntries.SingleAsync(item => item.ProjectId == project.Id);
        context.HobbyProjectEntries.Remove(entry);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await AssertVisibleAsync(false);
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
        result.Projects.Should().BeEmpty();
        result.Users.Should().BeEmpty();
        result.Tags.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 10, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, 101, "pageSize")]
    public async Task SearchPostsAsync_WhenPaginationIsInvalid_ThrowsDomainValidationException(int page, int pageSize, string expectedKey)
    {
        await using var context = AppDbContextFactory.Create();
        var service = new SearchService(context);

        var act = async () => await service.SearchPostsAsync("glow", tagSlug: null, page, pageSize);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().ContainKey(expectedKey);
    }

    [Fact]
    public async Task SearchUsersAsync_WhenPageSizeIsMaximum_IsAccepted()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new SearchService(context);

        var result = await service.SearchUsersAsync("glow", page: 1, pageSize: 100);

        result.PageSize.Should().Be(100);
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

    private static HobbyProject CreateProject(
        int id,
        ApplicationUser owner,
        Post? post,
        string title,
        string description)
    {
        var project = new HobbyProject
        {
            Id = id,
            OwnerUserId = owner.Id,
            OwnerUser = owner,
            Title = title,
            Description = description,
            Kind = HobbyProjectKinds.Army,
            Status = HobbyProjectStatuses.InProgress,
            CreatedUtc = DateTime.UtcNow.AddDays(-10),
            UpdatedUtc = DateTime.UtcNow.AddDays(-1)
        };

        if (post is not null)
        {
            project.Entries.Add(new HobbyProjectEntry
            {
                Project = project,
                Post = post,
                PostId = post.Id,
                LinkedUtc = post.CreatedUtc,
                ShowcaseOrder = 1
            });
        }

        return project;
    }
}
