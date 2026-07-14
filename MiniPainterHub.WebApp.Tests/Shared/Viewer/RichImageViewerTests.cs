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
    public void LoadingImageKeepsTheCloseActionAvailable()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find(".viewer-toolbar").ClassList.Should().Contain("is-image-loading");
        cut.Find("[data-testid='viewer-close']")
            .ClassList
            .Should()
            .Contain("viewer-toolbar__close--persistent");
    }

    [Fact]
    public void FailedImageKeepsCloseAndPagerButRemovesImageOnlyTools()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find("[data-testid='viewer-stage-image']").TriggerEvent("onerror", EventArgs.Empty);

        cut.WaitForAssertion(() =>
        {
            cut.Find(".viewer-toolbar").ClassList.Should().Contain("is-image-failed");
            cut.Find("[data-testid='viewer-image-error']").Should().NotBeNull();
            cut.Find("[data-testid='viewer-close']").Should().NotBeNull();
            cut.Find("[data-testid='viewer-stage-pager']").Should().NotBeNull();
            cut.Find(".viewer-toolbar__body").HasAttribute("hidden").Should().BeTrue();
        });
    }

    [Fact]
    public void AuthorNoteComposerUsesAClampedInsetAwayFromBothStagePagerControls()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var viewer = CreateViewer();
        viewer.AuthorMarks.Add(new AuthorMarkDto
        {
            Id = 501,
            PostImageId = 101,
            NormalizedX = 0.25m,
            NormalizedY = 0.5m,
            Tag = "Left-side note"
        });
        viewer.AuthorMarks.Add(new AuthorMarkDto
        {
            Id = 502,
            PostImageId = 101,
            NormalizedX = 0.75m,
            NormalizedY = 0.5m,
            Tag = "Right-side note"
        });

        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, viewer)
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        cut.Find("[data-testid='viewer-stage-image']").TriggerEvent("onload", EventArgs.Empty);
        var marks = cut.FindAll("[data-testid='viewer-author-mark']");

        marks[0].Click();
        cut.Find(".viewer-composer")
            .GetAttribute("style")
            .Should()
            .Be("right:clamp(4.5rem, 6vw, 6rem);bottom:1.25rem;");

        cut.FindAll("[data-testid='viewer-author-mark']")[1].Click();
        cut.Find(".viewer-composer")
            .GetAttribute("style")
            .Should()
            .Be("left:clamp(4.5rem, 6vw, 6rem);bottom:1.25rem;");
    }

    [Fact]
    public void ClickingAuthorNoteComposerCloseRemovesTheComposer()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var viewer = CreateViewer();
        viewer.AuthorMarks.Add(new AuthorMarkDto
        {
            Id = 501,
            PostImageId = 101,
            NormalizedX = 0.25m,
            NormalizedY = 0.5m,
            Tag = "Left-side note"
        });

        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, viewer)
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        cut.Find("[data-testid='viewer-stage-image']").TriggerEvent("onload", EventArgs.Empty);
        cut.Find("[data-testid='viewer-author-mark']").Click();
        cut.Find("[data-testid='viewer-mark-composer']").Should().NotBeNull();

        cut.Find("[data-testid='viewer-mark-close']").Click();

        cut.FindAll("[data-testid='viewer-mark-composer']").Should().BeEmpty();
    }

    [Fact]
    public void ClickingNewAuthorNoteComposerCloseRemovesTheDraft()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();
        var module = JSInterop.SetupModule("/JSHelpers/viewerInterop.js");
        module.Setup<ViewerRelativePoint>("getRelativePoint", _ => true)
            .SetResult(new ViewerRelativePoint { X = 480d, Y = 320d });
        module.Setup<ViewerRelativeRect>("getRelativeRect", _ => true)
            .SetResult(new ViewerRelativeRect
            {
                Left = 0d,
                Top = 50d,
                Width = 960d,
                Height = 540d
            });

        var viewer = CreateViewer();
        viewer.CanManageAuthorMarks = true;

        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, viewer)
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        cut.Find("[data-testid='viewer-stage-image']").TriggerEvent("onload", EventArgs.Empty);
        cut.Find("[data-testid='viewer-add-note']").Click();
        cut.Find("[data-testid='viewer-stage']").Click(new MouseEventArgs
        {
            ClientX = 480d,
            ClientY = 320d
        });
        cut.Find("[data-testid='viewer-mark-composer']").Should().NotBeNull();

        cut.Find("[data-testid='viewer-mark-close']").Click();

        cut.FindAll("[data-testid='viewer-mark-composer']").Should().BeEmpty();
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
        cut.Find("[data-testid='rich-image-viewer']").ClassList.Should().Contain("is-single-image");
        cut.FindAll("[data-testid='viewer-thumbnail-rail']").Should().BeEmpty();
        cut.Find("[data-testid='viewer-stage-image']").GetAttribute("alt").Should().Be("Moonlit skin experiment");
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
    public void MultiImageViewerUsesContextualImageAndThumbnailLabels()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find("[data-testid='viewer-stage-image']")
            .GetAttribute("alt")
            .Should()
            .Be("Moonlit skin experiment, image 1 of 2");

        cut.FindAll("[data-testid='viewer-thumbnail']")
            .Select(thumbnail => thumbnail.GetAttribute("aria-label"))
            .Should()
            .Equal(
                "View Moonlit skin experiment, image 1 of 2",
                "View Moonlit skin experiment, image 2 of 2");
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
    public void SingleFrameOmitsFilmstripButKeepsToolbarHints()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, CreateViewer(imageCount: 1))
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        cut.FindAll("[data-testid='viewer-thumbnail-rail']").Should().BeEmpty();
        cut.FindAll("[data-testid='viewer-thumbnail-static']").Should().BeEmpty();
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
