using System;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared.Viewer;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared.Viewer;

public class RichImageViewerTests : TestContext
{
    [Fact]
    public void LeftSwipeRequestsTheNextImage()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var requestedImageId = 0;
        var cut = RenderViewer(parameters => parameters
            .Add(component => component.ActiveImageIdChanged, EventCallback.Factory.Create<int>(this, imageId => requestedImageId = imageId)));

        var stage = cut.Find("[data-testid='viewer-stage']");
        stage.TriggerEvent("ontouchstart", CreateTouchEventArgs(280d, 180d));
        stage.TriggerEvent("ontouchend", CreateTouchEventArgs(180d, 186d, useChangedTouches: true));

        requestedImageId.Should().Be(102);
    }

    [Fact]
    public void ShortTouchGestureDoesNotRequestAnotherImage()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var requestedImageId = 0;
        var cut = RenderViewer(parameters => parameters
            .Add(component => component.ActiveImageIdChanged, EventCallback.Factory.Create<int>(this, imageId => requestedImageId = imageId)));

        var stage = cut.Find("[data-testid='viewer-stage']");
        stage.TriggerEvent("ontouchstart", CreateTouchEventArgs(280d, 180d));
        stage.TriggerEvent("ontouchend", CreateTouchEventArgs(238d, 182d, useChangedTouches: true));

        requestedImageId.Should().Be(0);
    }

    [Fact]
    public void ViewModeButtonsUpdateTheRenderedScale()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find("[data-testid='viewer-rail-zoom']").TextContent.Should().Contain("60%");
        cut.Find("[data-testid='viewer-reset']").ClassList.Should().Contain("is-active");

        cut.Find("[data-testid='viewer-view-fill']").Click();
        cut.Find("[data-testid='viewer-rail-zoom']").TextContent.Should().Contain("71%");
        cut.Find("[data-testid='viewer-view-fill']").ClassList.Should().Contain("is-active");

        cut.Find("[data-testid='viewer-view-actual']").Click();
        cut.Find("[data-testid='viewer-rail-zoom']").TextContent.Should().Contain("100%");
        cut.Find("[data-testid='viewer-view-actual']").ClassList.Should().Contain("is-active");
    }

    [Fact]
    public void StageNavigationArrowsRequestAdjacentImages()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var requestedImageId = 0;
        var cut = RenderViewer(parameters => parameters
            .Add(component => component.ActiveImageIdChanged, EventCallback.Factory.Create<int>(this, imageId => requestedImageId = imageId)));

        cut.Find("[data-testid='viewer-stage-next']").Click();
        requestedImageId.Should().Be(102);

        cut.Find("[data-testid='viewer-stage-prev']").Click();
        requestedImageId.Should().Be(101);
    }

    [Fact]
    public void SingleFrameViewerOmitsStageNavigationArrows()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, CreateViewer(imageCount: 1))
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        cut.FindAll("[data-testid='viewer-stage-prev']").Should().BeEmpty();
        cut.FindAll("[data-testid='viewer-stage-next']").Should().BeEmpty();
    }

    [Fact]
    public void ViewerStageUsesPreviewImageUntilActualSizeIsRequested()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find("[data-testid='viewer-stage-image']")
            .GetAttribute("src")
            .Should()
            .Be("/images/moonlit-skin-1-preview.png");

        cut.Find("[data-testid='viewer-view-actual']").Click();

        cut.Find("[data-testid='viewer-stage-image']")
            .GetAttribute("src")
            .Should()
            .Be("/images/moonlit-skin-1-full.png");
    }

    [Fact]
    public void ViewerPreloadSourceUsesPreviewImageOnly()
    {
        var source = ViewerImageLoadState.GetPreloadSource(new PostViewerImageDto
        {
            ImageUrl = "/images/moonlit-skin-1-full.png",
            PreviewUrl = "/images/moonlit-skin-1-preview.png"
        });

        source.Should().Be("/images/moonlit-skin-1-preview.png");
    }

    [Fact]
    public async Task StageResizeRecomputesFitBoxForTheLatestViewport()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        await cut.Instance.OnStageResized(369d, 231d);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='viewer-stage-fitbox']")
                .GetAttribute("style")
                .Should()
                .Contain("width:369.00px")
                .And.Contain("height:207.56px");
        });

        await cut.Instance.OnStageResized(1600d, 1152d);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='viewer-stage-fitbox']")
                .GetAttribute("style")
                .Should()
                .Contain("width:1600.00px")
                .And.Contain("height:900.00px");
        });
    }

    [Fact]
    public async Task StageChromeTracksRenderedPortraitImageEdgeForActionControls()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, CreateViewer(imageCount: 1, width: 900, height: 1600))
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        await cut.Instance.OnStageResized(1200d, 900d);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='viewer-stage-chrome']")
                .GetAttribute("style")
                .Should()
                .Contain("--viewer-action-image-right:853.12px")
                .And.Contain("--viewer-action-image-bottom:900.00px");
        });

        await cut.Instance.OnViewerTransformSettled(1.25d, 0d, -30d);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='viewer-stage-chrome']")
                .GetAttribute("style")
                .Should()
                .Contain("--viewer-action-image-right:916.41px")
                .And.Contain("--viewer-action-image-bottom:982.50px");
        });
    }

    [Fact]
    public void ViewerShellDoesNotBindArtworkAsDynamicBlurredBackdrop()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find(".viewer-shell__backdrop")
            .HasAttribute("style")
            .Should()
            .BeFalse();
    }

    [Fact]
    public void RailDoesNotRenderCollapseToggleAndKeepsTheFilmstripVisible()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find("[data-testid='viewer-thumbnail-rail']");
        cut.FindAll("[data-testid='viewer-rail-toggle']").Should().BeEmpty();
        cut.Find("[data-testid='viewer-close']").Should().NotBeNull();
    }

    [Fact]
    public void FilmstripShowsCompactShortcutHints()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find("[data-testid='viewer-shortcuts']").TextContent.Should().Contain("navigate");
        cut.Find("[data-testid='viewer-shortcuts']").TextContent.Should().Contain("zoom");
        cut.Find("[data-testid='viewer-shortcuts']").TextContent.Should().Contain("Esc");
    }

    [Fact]
    public void SingleFrameRailKeepsPreviewRailOmitsBrowseGroupAndShowsHints()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, CreateViewer(imageCount: 1))
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        cut.Find("[data-testid='viewer-thumbnail-rail']").TextContent.Should().Contain("Preview");
        cut.FindAll("[data-testid='viewer-rail-navigation']").Should().BeEmpty();
        cut.FindAll("[data-testid='viewer-shortcuts']").Should().BeEmpty();
        cut.Find("[data-testid='viewer-rail-hints']").TextContent.Should().Contain("zoom");
        cut.Find("[data-testid='viewer-rail-hints']").TextContent.Should().Contain("close");
    }

    [Fact]
    public void ReopeningViewerResetsScaleModeAndZoomToFitForTheSameImage()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find("[data-testid='viewer-view-actual']").Click();
        cut.Find("[data-testid='viewer-zoom-in']").Click();
        cut.Find("[data-testid='viewer-rail-zoom']").TextContent.Should().Contain("125%");

        cut.SetParametersAndRender(parameters => ConfigureViewerParameters(parameters, false));
        cut.SetParametersAndRender(parameters => ConfigureViewerParameters(parameters, true));

        cut.Find("[data-testid='viewer-rail-zoom']").TextContent.Should().Contain("60%");
        cut.Find("[data-testid='viewer-reset']").ClassList.Should().Contain("is-active");
    }

    [Fact]
    public void ViewerInteropInitializationRunsOnlyOnOpenTransition()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find("[data-testid='viewer-view-fill']").Click();
        cut.Find("[data-testid='viewer-view-actual']").Click();
        cut.Render();

        JSInterop.Invocations
            .Count(invocation => invocation.Identifier == "isFullscreenSupported")
            .Should()
            .Be(1);
    }

    private IRenderedComponent<RichImageViewer> RenderViewer(Action<ComponentParameterCollectionBuilder<RichImageViewer>>? configure = null)
    {
        return RenderComponent<RichImageViewer>(parameters =>
        {
            ConfigureViewerParameters(parameters, true);
            configure?.Invoke(parameters);
        });
    }

    private static void ConfigureViewerParameters(ComponentParameterCollectionBuilder<RichImageViewer> parameters, bool isOpen)
    {
        parameters
            .Add(component => component.IsOpen, isOpen)
            .Add(component => component.Viewer, CreateViewer())
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { }));
    }

    private static PostViewerDto CreateViewer(int imageCount = 2, int width = 1600, int height = 900)
    {
        var viewer = new PostViewerDto
        {
            PostId = 19,
            Title = "Moonlit skin experiment",
            CreatedById = "author-1",
            AuthorName = "Mira Sol",
            CreatedAt = new DateTime(2026, 3, 13, 19, 30, 0, DateTimeKind.Utc)
        };

        viewer.Images.Add(new PostViewerImageDto
        {
            Id = 101,
            ImageUrl = "/images/moonlit-skin-1-full.png",
            PreviewUrl = "/images/moonlit-skin-1-preview.png",
            ThumbnailUrl = "/images/moonlit-skin-1-thumb.png",
            Width = width,
            Height = height
        });

        if (imageCount > 1)
        {
            viewer.Images.Add(new PostViewerImageDto
            {
                Id = 102,
                ImageUrl = "/images/moonlit-skin-2-full.png",
                PreviewUrl = "/images/moonlit-skin-2-preview.png",
                ThumbnailUrl = "/images/moonlit-skin-2-thumb.png",
                Width = 1600,
                Height = 900
            });
        }

        return viewer;
    }

    private static TouchEventArgs CreateTouchEventArgs(double clientX, double clientY, bool useChangedTouches = false)
    {
        var point = new TouchPoint
        {
            ClientX = clientX,
            ClientY = clientY
        };

        return new TouchEventArgs
        {
            Touches = useChangedTouches ? Array.Empty<TouchPoint>() : new[] { point },
            ChangedTouches = useChangedTouches ? new[] { point } : Array.Empty<TouchPoint>()
        };
    }
}
