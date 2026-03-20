using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
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
}
