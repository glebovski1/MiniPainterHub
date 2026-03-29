using System;
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
        stage.TriggerEvent("ontouchmove", CreateTouchEventArgs(180d, 186d));
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
        stage.TriggerEvent("ontouchmove", CreateTouchEventArgs(238d, 182d));
        stage.TriggerEvent("ontouchend", CreateTouchEventArgs(238d, 182d, useChangedTouches: true));

        requestedImageId.Should().Be(0);
    }

    [Fact]
    public void ViewModeButtonsUpdateTheRenderedScale()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderViewer();

        cut.Find("[data-testid='viewer-control-rail']").TextContent.Should().Contain("60%");
        cut.Find("[data-testid='viewer-reset']").ClassList.Should().Contain("is-active");

        cut.Find("[data-testid='viewer-view-fill']").Click();
        cut.Find("[data-testid='viewer-control-rail']").TextContent.Should().Contain("71%");
        cut.Find("[data-testid='viewer-view-fill']").ClassList.Should().Contain("is-active");

        cut.Find("[data-testid='viewer-view-actual']").Click();
        cut.Find("[data-testid='viewer-control-rail']").TextContent.Should().Contain("100%");
        cut.Find("[data-testid='viewer-view-actual']").ClassList.Should().Contain("is-active");
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
        cut.Find("[data-testid='viewer-stage']").TriggerEvent("onwheel", new WheelEventArgs { DeltaY = -1 });
        cut.Find("[data-testid='viewer-control-rail']").TextContent.Should().Contain("125%");

        cut.SetParametersAndRender(parameters => ConfigureViewerParameters(parameters, false));
        cut.SetParametersAndRender(parameters => ConfigureViewerParameters(parameters, true));

        cut.Find("[data-testid='viewer-control-rail']").TextContent.Should().Contain("60%");
        cut.Find("[data-testid='viewer-reset']").ClassList.Should().Contain("is-active");
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

    private static PostViewerDto CreateViewer(int imageCount = 2)
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
            Width = 1600,
            Height = 900
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
