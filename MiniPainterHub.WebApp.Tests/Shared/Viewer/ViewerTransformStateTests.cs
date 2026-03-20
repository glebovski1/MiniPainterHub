using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared.Viewer;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared.Viewer;

public class ViewerTransformStateTests
{
    [Theory]
    [InlineData(900, 1600, 450, 800)]
    [InlineData(1000, 1500, 533.3333, 800)]
    [InlineData(1200, 1200, 800, 800)]
    [InlineData(1600, 1200, 1066.6667, 800)]
    [InlineData(1600, 900, 1200, 675)]
    [InlineData(2100, 900, 1200, 514.2857)]
    public void GetFitRectPreservesAspectRatioAcrossTheViewerMatrix(
        int imageWidth,
        int imageHeight,
        double expectedWidth,
        double expectedHeight)
    {
        var state = new ViewerTransformState();
        state.SetStage(1200, 800, CreateImage(imageWidth, imageHeight));

        var fit = state.GetFitRect(CreateImage(imageWidth, imageHeight));

        fit.Width.Should().BeApproximately(expectedWidth, 0.2);
        fit.Height.Should().BeApproximately(expectedHeight, 0.2);
        (fit.Width / fit.Height).Should().BeApproximately((double)imageWidth / imageHeight, 0.001);
        fit.Left.Should().BeGreaterThanOrEqualTo(0);
        fit.Top.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void CenterOnPromotesZoomAndKeepsTheTargetWithinTheStageBounds()
    {
        var image = CreateImage(2100, 900);
        var state = new ViewerTransformState();
        state.SetStage(1200, 800, image);

        state.CenterOn(0.75m, 0.6m, promoteZoom: true, image);

        state.Zoom.Should().BeGreaterThan(1d);
        state.CanPan.Should().BeTrue();
        state.PanX.Should().BeLessThan(0);
        state.PanY.Should().BeLessThanOrEqualTo(0);
    }

    [Fact]
    public void PanByIsClampedToTheScaledFitRect()
    {
        var image = CreateImage(1600, 900);
        var state = new ViewerTransformState();
        state.SetStage(1200, 800, image);
        state.SetZoom(2d, image);

        state.PanBy(10_000, 10_000, image);

        var fit = state.GetFitRect(image);
        var maxPanX = ((fit.Width * state.Zoom) - state.StageWidth) / 2d;
        var maxPanY = ((fit.Height * state.Zoom) - state.StageHeight) / 2d;

        state.PanX.Should().BeApproximately(maxPanX, 0.01);
        state.PanY.Should().BeApproximately(maxPanY, 0.01);
    }

    private static PostViewerImageDto CreateImage(int width, int height) =>
        new()
        {
            Id = width + height,
            ImageUrl = $"/fixtures/{width}x{height}.png",
            Width = width,
            Height = height
        };
}
