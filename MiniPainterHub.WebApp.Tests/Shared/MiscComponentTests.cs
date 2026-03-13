using System.Collections.Generic;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class MiscComponentTests : TestContext
{
    [Fact]
    public void ImageCarousel_WhenSlidesAreMissing_RendersEmptyState()
    {
        var cut = RenderComponent<ImageCarousel>();

        cut.Markup.Should().Contain("No slides.");
    }

    [Fact]
    public void ImageCarousel_WhenSingleSlide_RendersCaptionAndLinkTarget()
    {
        var cut = RenderComponent<ImageCarousel>(parameters => parameters
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
        var cut = RenderComponent<TagBadgeList>(parameters => parameters
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

        var cut = RenderComponent<UserPanelOverlay>();

        cut.Markup.Should().Contain("userPanelOffcanvas");
        cut.Markup.Should().Contain("My panel");
    }

    [Fact]
    public void UserPanelOverlay_WhenAnonymous_DoesNotRenderOffcanvasPanel()
    {
        this.AddTestAuthorization();

        var cut = RenderComponent<UserPanelOverlay>();

        cut.Markup.Should().NotContain("userPanelOffcanvas");
    }
}
