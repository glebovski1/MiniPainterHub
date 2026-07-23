using System.Collections.Generic;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class MiscComponentTests : BunitContext
{
    [Fact]
    public void PageHero_RendersStudioPrimitiveSlotsAndClasses()
    {
        var cut = Render<PageHero>(parameters => parameters
            .Add(component => component.Eyebrow, "Studio")
            .Add(component => component.Title, "Workbench")
            .Add(component => component.Description, "A focused workspace.")
            .Add(component => component.Actions, (RenderFragment)(builder =>
            {
                builder.AddMarkupContent(0, "<button class=\"btn btn-primary\">Create</button>");
            }))
            .Add(component => component.Metrics, (RenderFragment)(builder =>
            {
                builder.AddMarkupContent(0, "<div class=\"metric-chip\">3 posts</div>");
            })));

        cut.Find(".page-hero").Should().NotBeNull();
        cut.Find(".page-hero__header--with-actions").Should().NotBeNull();
        cut.Find(".page-hero__actions .btn").TextContent.Should().Contain("Create");
        cut.Find(".page-hero__support .metric-chip").TextContent.Should().Contain("3 posts");
    }

    [Fact]
    public void SectionHeader_RendersContentAndActionRegions()
    {
        var cut = Render<SectionHeader>(parameters => parameters
            .Add(component => component.Eyebrow, "Queue")
            .Add(component => component.Title, "Latest work")
            .Add(component => component.Description, "Fresh from the studio.")
            .Add(component => component.Actions, (RenderFragment)(builder =>
            {
                builder.AddMarkupContent(0, "<a class=\"btn btn-outline-secondary\" href=\"/posts/all\">View all</a>");
            })));

        cut.Find(".section-heading__content").TextContent.Should().Contain("Latest work");
        cut.Find(".section-heading__actions .btn").TextContent.Should().Contain("View all");
    }

    [Fact]
    public void EmptyState_RendersConfiguredStructureAndTestId()
    {
        var cut = Render<EmptyState>(parameters => parameters
            .Add(component => component.IconClass, "bi bi-brush")
            .Add(component => component.Title, "No work yet")
            .Add(component => component.Description, "Start with a first post.")
            .Add(component => component.TestId, "empty-work"));

        cut.Find("[data-testid='empty-work']").ClassList.Should().Contain("empty-state");
        cut.Find(".empty-state__icon i").ClassList.Should().Contain("bi-brush");
        cut.Find(".empty-state__title").TextContent.Should().Be("No work yet");
    }

    [Fact]
    public void ImageCarousel_WhenSlidesAreMissing_RendersEmptyState()
    {
        var cut = Render<ImageCarousel>();

        cut.Markup.Should().Contain("No slides.");
    }

    [Fact]
    public void ImageCarousel_WhenSingleSlide_RendersCaptionAndLinkTarget()
    {
        var cut = Render<ImageCarousel>(parameters => parameters
            .Add(component => component.Slides, new List<ImageCarousel.Slide>
            {
                new("/images/post.webp", "/posts/42", "Spotlight")
            })
            .Add(component => component.OpenInNewTab, true));

        cut.Find("img").GetAttribute("src").Should().Be("/images/post.webp");
        cut.Find("img").GetAttribute("alt").Should().Be("Spotlight");
        cut.Find("a").GetAttribute("target").Should().Be("_blank");
        cut.Markup.Should().Contain("Spotlight");
    }

    [Fact]
    public void TagBadgeList_WhenTagClicked_NavigatesToEncodedSearchRoute()
    {
        var cut = Render<TagBadgeList>(parameters => parameters
            .Add(component => component.Tags, new[]
            {
                new TagDto { Name = "Glazing Basics", Slug = "glazing-basics" }
            }));

        cut.Find("[data-testid='tag-link']").Click();

        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri
            .Should().Be("http://localhost/search?q=Glazing%20Basics&tab=posts&tag=glazing-basics");
    }

    [Fact]
    public void UserPanelOverlay_WhenAuthorized_RendersOffcanvasPanel()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddConversationStub();

        var cut = Render<UserPanelOverlay>();

        cut.Markup.Should().Contain("userPanelOffcanvas");
        cut.Markup.Should().Contain("My panel");
    }

    [Fact]
    public void UserPanelOverlay_WhenAnonymous_DoesNotRenderOffcanvasPanel()
    {
        this.AddAuthorization();

        var cut = Render<UserPanelOverlay>();

        cut.Markup.Should().NotContain("userPanelOffcanvas");
    }
}
