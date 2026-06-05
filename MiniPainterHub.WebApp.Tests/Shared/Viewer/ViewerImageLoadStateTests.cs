using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared.Viewer;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared.Viewer;

public class ViewerImageLoadStateTests
{
    [Fact]
    public void GetCurrentSource_UsesPreviewUntilActualSizeMode()
    {
        var image = CreateImage(1, previewUrl: "/images/preview.png");
        var state = new ViewerImageLoadState();

        state.GetCurrentSource(image, ViewerScaleMode.Fit).Should().Be("/images/preview.png");
        state.GetCurrentSource(image, ViewerScaleMode.Fill).Should().Be("/images/preview.png");
        state.GetCurrentSource(image, ViewerScaleMode.ActualSize).Should().Be("/images/full.png");
    }

    [Fact]
    public void Retry_AppendsStableIncreasingRetryTokenToSelectedImage()
    {
        var image = CreateImage(1, "/images/full.png?size=large", "/images/preview.png");
        var other = CreateImage(2);
        var state = new ViewerImageLoadState();

        state.Retry(image.Id);
        state.Retry(image.Id);

        state.GetCurrentSource(image, ViewerScaleMode.ActualSize).Should().Be("/images/full.png?size=large&retry=2");
        state.GetCurrentSource(other, ViewerScaleMode.ActualSize).Should().Be("/images/full.png");
        state.IsLoading(image).Should().BeTrue();
    }

    [Fact]
    public void GetAdjacentPreloadSources_UsesPreviewUrlsAndDeduplicates()
    {
        var images = new[]
        {
            CreateImage(101, previewUrl: "/images/first-preview.png"),
            CreateImage(102, previewUrl: "/images/second-preview.png"),
            CreateImage(103, previewUrl: "/images/first-preview.png")
        };

        var urls = ViewerImageLoadState.GetAdjacentPreloadSources(images, currentImageId: 101);

        urls.Should().Equal("/images/first-preview.png", "/images/second-preview.png");
    }

    private static PostViewerImageDto CreateImage(int id, string imageUrl = "/images/full.png", string? previewUrl = null) =>
        new()
        {
            Id = id,
            ImageUrl = imageUrl,
            PreviewUrl = previewUrl
        };
}
