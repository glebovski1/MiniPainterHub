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
        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, CreateViewer())
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.ActiveImageIdChanged, EventCallback.Factory.Create<int>(this, imageId => requestedImageId = imageId))
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

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
        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, CreateViewer())
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.ActiveImageIdChanged, EventCallback.Factory.Create<int>(this, imageId => requestedImageId = imageId))
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        var stage = cut.Find("[data-testid='viewer-stage']");
        stage.TriggerEvent("ontouchstart", CreateTouchEventArgs(280d, 180d));
        stage.TriggerEvent("ontouchmove", CreateTouchEventArgs(238d, 182d));
        stage.TriggerEvent("ontouchend", CreateTouchEventArgs(238d, 182d, useChangedTouches: true));

        requestedImageId.Should().Be(0);
    }

    [Fact]
    public void ReopeningViewerResetsZoomToFitForTheSameImage()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorMarkStub();

        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, CreateViewer())
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        var stage = cut.Find("[data-testid='viewer-stage']");
        stage.TriggerEvent("onwheel", new WheelEventArgs { DeltaY = -1 });

        cut.Find("[data-testid='viewer-control-rail']").TextContent.Should().Contain("125%");

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.IsOpen, false)
            .Add(component => component.Viewer, CreateViewer())
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.Viewer, CreateViewer())
            .Add(component => component.ActiveImageId, 101)
            .Add(component => component.SidePanelContent, (RenderFragment)(_ => { })));

        cut.Find("[data-testid='viewer-control-rail']").TextContent.Should().Contain("100%");
    }

    private static PostViewerDto CreateViewer()
    {
        return new PostViewerDto
        {
            PostId = 19,
            Title = "Moonlit skin experiment",
            CreatedById = "author-1",
            AuthorName = "Mira Sol",
            CreatedAt = new DateTime(2026, 3, 13, 19, 30, 0, DateTimeKind.Utc),
            Images =
            {
                new PostViewerImageDto
                {
                    Id = 101,
                    ImageUrl = "/images/moonlit-skin-1-full.png",
                    PreviewUrl = "/images/moonlit-skin-1-preview.png",
                    ThumbnailUrl = "/images/moonlit-skin-1-thumb.png",
                    Width = 1600,
                    Height = 900
                },
                new PostViewerImageDto
                {
                    Id = 102,
                    ImageUrl = "/images/moonlit-skin-2-full.png",
                    PreviewUrl = "/images/moonlit-skin-2-preview.png",
                    ThumbnailUrl = "/images/moonlit-skin-2-thumb.png",
                    Width = 1600,
                    Height = 900
                }
            }
        };
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
