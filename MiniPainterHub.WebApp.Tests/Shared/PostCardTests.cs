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

public class PostCardTests : TestContext
{
    [Fact]
    public void Image_WhenThumbnailFails_FallsBackToFullImageUrl()
    {
        this.AddTestAuthorization();
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
        var auth = this.AddTestAuthorization();
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
        var auth = this.AddTestAuthorization();
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
    public void WhenHiddenPost_RendersHiddenBadge()
    {
        var auth = this.AddTestAuthorization();
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

    private IRenderedFragment RenderWithAuth(PostSummaryDto post)
    {
        return Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<PostCard>(0);
                childBuilder.AddAttribute(1, nameof(PostCard.Post), post);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }
}
