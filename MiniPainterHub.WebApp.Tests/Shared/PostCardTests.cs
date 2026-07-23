using System;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class PostCardTests : BunitContext
{
    [Fact]
    public void RendersAsOwnedArticleWithoutBootstrapCardShell()
    {
        this.AddAuthorization();
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 40,
            Title = "Studio card",
            Snippet = "A cleaner post card.",
            AuthorName = "author",
            AuthorId = "author-1",
            CreatedAt = DateTime.UtcNow
        };

        var cut = RenderWithAuth(post);

        cut.Find(".post-card").TagName.Should().Be("ARTICLE");
        cut.FindAll(".card-body").Should().BeEmpty();
        cut.FindAll(".card-footer").Should().BeEmpty();
        cut.Find(".post-card__title").TextContent.Should().Be("Studio card");
    }

    [Fact]
    public void Image_WhenThumbnailUrlIsProvided_UsesThumbnailUrl()
    {
        this.AddAuthorization();
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 41,
            Title = "Title",
            Snippet = "Snippet",
            AuthorName = "author",
            AuthorId = "author-1",
            CreatedAt = DateTime.UtcNow,
            ImageUrl = "/uploads/images/full.png",
            ThumbnailUrl = "/uploads/images/thumb.webp"
        };

        var cut = RenderWithAuth(post);

        var image = cut.Find("img");
        image.GetAttribute("src").Should().Be("http://localhost/uploads/images/thumb.webp");
        image.GetAttribute("alt").Should().Be("Title by author");
    }

    [Fact]
    public void WithoutImage_RendersCompactTextLedCard()
    {
        this.AddAuthorization();
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 45,
            Title = "Paint recipe without a photo",
            Snippet = "A useful note can stand on its own.",
            AuthorName = "Painter",
            AuthorId = "painter-1",
            CreatedAt = DateTime.UtcNow
        };

        var cut = RenderWithAuth(post);

        cut.Find(".post-card").ClassList.Should().Contain("post-card--text-only");
        cut.FindAll(".post-card__placeholder").Should().BeEmpty();
        cut.Markup.Should().NotContain("No preview image");
        cut.Find(".post-card__title").TextContent.Should().Be(post.Title);
    }

    [Fact]
    public void Image_WhenPriorityImage_UsesEagerHighPriorityLoading()
    {
        this.AddAuthorization();
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 43,
            Title = "Priority title",
            Snippet = "Priority snippet",
            AuthorName = "author",
            AuthorId = "author-1",
            CreatedAt = DateTime.UtcNow,
            ImageUrl = "/uploads/images/full.png",
            ThumbnailUrl = "/uploads/images/thumb.webp"
        };

        var cut = RenderWithAuth(post, isPriorityImage: true);

        var image = cut.Find("img");
        image.GetAttribute("loading").Should().Be("eager");
        image.GetAttribute("fetchpriority").Should().Be("high");
    }

    [Fact]
    public void Image_WhenNotPriorityImage_UsesLazyAutoPriorityLoading()
    {
        this.AddAuthorization();
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 44,
            Title = "Lazy title",
            Snippet = "Lazy snippet",
            AuthorName = "author",
            AuthorId = "author-1",
            CreatedAt = DateTime.UtcNow,
            ImageUrl = "/uploads/images/full.png",
            ThumbnailUrl = "/uploads/images/thumb.webp"
        };

        var cut = RenderWithAuth(post);

        var image = cut.Find("img");
        image.GetAttribute("loading").Should().Be("lazy");
        image.GetAttribute("fetchpriority").Should().Be("auto");
    }

    [Fact]
    public void Image_WhenThumbnailFails_FallsBackToFullImageUrl()
    {
        this.AddAuthorization();
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 42,
            Title = "Title",
            Snippet = "Snippet",
            AuthorName = "author",
            AuthorId = "author-1",
            CreatedAt = DateTime.UtcNow,
            ImageUrl = "/uploads/images/0000002a-0000-0000-0000-000000000000/11111111-1111-1111-1111-111111111111_max.webp"
        };

        var cut = RenderWithAuth(post);
        var image = cut.Find("img");
        image.GetAttribute("src").Should().Contain("_thumb.");

        image.TriggerEvent("onerror", EventArgs.Empty);

        image = cut.Find("img");
        image.GetAttribute("src").Should().Contain("_max.");
    }

    [Fact]
    public void WhenAdminRole_RendersPostIdBadge()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 7,
            Title = "Title",
            Snippet = "Snippet",
            AuthorName = "author",
            AuthorId = "author-1",
            CreatedAt = DateTime.UtcNow,
            ImageUrl = null
        };

        var cut = RenderWithAuth(post);

        cut.Find("[data-testid='post-id-badge']").TextContent.Should().Be("#7");
    }

    [Fact]
    public void WhenPostHasTags_RendersTagBadges()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("reader");
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 9,
            Title = "Title",
            Snippet = "Snippet",
            AuthorName = "author",
            AuthorId = "author-1",
            CreatedAt = DateTime.UtcNow,
            Tags = new()
            {
                new TagDto { Name = "glazing", Slug = "glazing" },
                new TagDto { Name = "nmm", Slug = "nmm" }
            }
        };

        var cut = RenderWithAuth(post);

        cut.Find("[data-testid='post-card-tags']").TextContent.Should().Contain("#glazing");
        cut.Find("[data-testid='post-card-tags']").TextContent.Should().Contain("#nmm");
    }

    [Fact]
    public void WhenPostHasRecipeFields_RendersRecipeSummary()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("reader");
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 10,
            Title = "Title",
            Snippet = "Snippet",
            AuthorName = "author",
            AuthorId = "author-1",
            CreatedAt = DateTime.UtcNow,
            MiniatureName = "Crimson Captain",
            Difficulty = "Intermediate",
            Techniques = "Glazing"
        };

        var cut = RenderWithAuth(post);

        var recipe = cut.Find("[data-testid='post-card-recipe']").TextContent;
        recipe.Should().Contain("Crimson Captain");
        recipe.Should().Contain("Intermediate");
        recipe.Should().Contain("Glazing");
    }

    [Fact]
    public void WhenPostBelongsToPublicProject_RendersProjectLink()
    {
        this.AddAuthorization();
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 12,
            Title = "First squad complete",
            Snippet = "A diary update.",
            AuthorName = "Painter",
            AuthorId = "painter-1",
            CreatedAt = DateTime.UtcNow,
            Project = new HobbyProjectReferenceDto
            {
                Id = 5,
                Title = "Swamp warband",
                IsPublic = true
            }
        };

        var cut = RenderWithAuth(post);

        var link = cut.Find("[data-testid='post-card-project-link']");
        link.TextContent.Should().Contain("Part of Swamp warband");
        link.GetAttribute("href").Should().Be("/projects/5");
    }

    [Fact]
    public void WhenProjectIsNotPublic_DoesNotRenderProjectLink()
    {
        this.AddAuthorization();
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 13,
            Title = "Private setup",
            Snippet = "Not public yet.",
            AuthorName = "Painter",
            AuthorId = "painter-1",
            CreatedAt = DateTime.UtcNow,
            Project = new HobbyProjectReferenceDto { Id = 6, Title = "Setup", IsPublic = false }
        };

        var cut = RenderWithAuth(post);

        cut.FindAll("[data-testid='post-card-project-link']").Should().BeEmpty();
    }

    [Fact]
    public void WhenHiddenPost_RendersHiddenBadge()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");
        this.AddModerationStub();

        var post = new PostSummaryDto
        {
            Id = 8,
            Title = "Title",
            Snippet = "Snippet",
            AuthorName = "author",
            AuthorId = "author-1",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = true
        };

        var cut = RenderWithAuth(post);

        cut.Find("[data-testid='post-hidden-badge']").TextContent.Should().Contain("Hidden");
        cut.FindAll("[data-testid='post-card-restore']").Should().HaveCount(1);
    }

    private IRenderedComponent<IComponent> RenderWithAuth(PostSummaryDto post, bool isPriorityImage = false)
    {
        return Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<PostCard>(0);
                childBuilder.AddAttribute(1, nameof(PostCard.Post), post);
                childBuilder.AddAttribute(2, nameof(PostCard.IsPriorityImage), isPriorityImage);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }
}
