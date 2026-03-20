using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared.Viewer;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class RichImageViewerTests : TestContext
{
    [Fact]
    public async Task WhenImageMetadataIsMissing_UsesInferredNaturalDimensionsForFitBox()
    {
        this.AddAuthorMarkStub();

        JSInterop.Mode = JSRuntimeMode.Strict;
        var module = JSInterop.SetupModule("./JSHelpers/viewerInterop.js");
        module.Setup<bool>("isFullscreenSupported").SetResult(false);
        module.SetupVoid("observeStageSize", _ => true);
        module.SetupVoid("registerFullscreenChange", _ => true);
        module.SetupVoid("preloadImages", _ => true);
        module.Setup<ImageDimensionsSnapshot>("getImageDimensions", _ => true)
            .SetResult(new ImageDimensionsSnapshot { Width = 900, Height = 1600 });

        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(viewer => viewer.Viewer, new PostViewerDto
            {
                PostId = 42,
                Images = new List<PostViewerImageDto>
                {
                    new()
                    {
                        Id = 7001,
                        ImageUrl = "/images/legacy-max.png",
                        PreviewUrl = "/images/legacy-preview.png",
                        ThumbnailUrl = "/images/legacy-thumb.png"
                    }
                }
            }));

        await cut.Find("[data-testid='post-details-image']").TriggerEventAsync("onload", new EventArgs());

        cut.WaitForAssertion(() =>
        {
            cut.Find(".viewer-stage__fitbox")
                .GetAttribute("style")
                .Should()
                .Be("left:300.00px;top:0.00px;width:360.00px;height:640.00px;");
        });
    }

    [Fact]
    public async Task WhenPlacementOccursAfterZoomAndPan_NormalizesAgainstTransformedImage()
    {
        this.AddAuthorMarkStub();

        ViewerMarkDraftDto? placedDraft = null;

        JSInterop.Mode = JSRuntimeMode.Strict;
        var module = JSInterop.SetupModule("./JSHelpers/viewerInterop.js");
        module.Setup<bool>("isFullscreenSupported").SetResult(false);
        module.SetupVoid("observeStageSize", _ => true);
        module.SetupVoid("registerFullscreenChange", _ => true);
        module.SetupVoid("preloadImages", _ => true);
        module.Setup<ViewerRelativePoint>("getRelativePoint", _ => true)
            .SetResult(new ViewerRelativePoint { X = 744d, Y = 133.75d });

        var cut = RenderComponent<RichImageViewer>(parameters => parameters
            .Add(viewer => viewer.Viewer, new PostViewerDto
            {
                PostId = 43,
                Images = new List<PostViewerImageDto>
                {
                    new()
                    {
                        Id = 7002,
                        ImageUrl = "/images/fitted-max.png",
                        PreviewUrl = "/images/fitted-preview.png",
                        ThumbnailUrl = "/images/fitted-thumb.png",
                        Width = 1600,
                        Height = 900
                    }
                }
            })
            .Add(viewer => viewer.IsCommentPlacementMode, true)
            .Add(viewer => viewer.OnCommentPlacementSelected, EventCallback.Factory.Create<ViewerMarkDraftDto>(this, draft => placedDraft = draft)));

        var stage = cut.Find("[data-testid='viewer-stage']");

        await stage.TriggerEventAsync("onwheel", new WheelEventArgs { DeltaY = -1 });
        await stage.TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });
        await stage.TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });

        cut.WaitForAssertion(() =>
        {
            cut.Find(".viewer-stage__transform")
                .GetAttribute("style")
                .Should()
                .Contain("translate(-36.00px,-17.50px) scale(1.25)");
        });

        await stage.TriggerEventAsync("onpointerdown", new PointerEventArgs { ClientX = 744d, ClientY = 133.75d });
        await stage.TriggerEventAsync("onpointerup", new PointerEventArgs { ClientX = 744d, ClientY = 133.75d });

        cut.WaitForAssertion(() =>
        {
            placedDraft.Should().NotBeNull();
            placedDraft!.NormalizedX.Should().Be(0.75m);
            placedDraft.NormalizedY.Should().Be(0.25m);
        });
    }
}
