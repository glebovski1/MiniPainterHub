using System;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared.Viewer;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared.Viewer;

public class ViewerTransformStateTests
{
    [Theory]
    [InlineData(900, 1600)]
    [InlineData(1000, 1500)]
    [InlineData(1200, 1200)]
    [InlineData(1600, 1200)]
    [InlineData(1600, 900)]
    [InlineData(2100, 900)]
    public void FitModeUsesContainMathAcrossTheViewerMatrix(int imageWidth, int imageHeight)
    {
        var image = CreateImage(imageWidth, imageHeight);
        var state = new ViewerTransformState();
        state.SetStage(1200, 800, image);

        var fit = state.GetImageRect(image);
        var expectedScale = Math.Min(1200d / imageWidth, 800d / imageHeight);

        fit.Width.Should().BeApproximately(imageWidth * expectedScale, 0.2);
        fit.Height.Should().BeApproximately(imageHeight * expectedScale, 0.2);
        fit.Left.Should().BeApproximately((1200d - fit.Width) / 2d, 0.2);
        fit.Top.Should().BeApproximately((800d - fit.Height) / 2d, 0.2);
        state.ScaleMode.Should().Be(ViewerScaleMode.Fit);
        state.CanPan.Should().BeFalse();
    }

    [Theory]
    [InlineData(900, 1600)]
    [InlineData(1000, 1500)]
    [InlineData(1200, 1200)]
    [InlineData(1600, 1200)]
    [InlineData(1600, 900)]
    [InlineData(2100, 900)]
    public void FillModeUsesCoverMathAcrossTheViewerMatrix(int imageWidth, int imageHeight)
    {
        var image = CreateImage(imageWidth, imageHeight);
        var state = new ViewerTransformState();
        state.SetStage(1200, 800, image);
        state.SetScaleMode(ViewerScaleMode.Fill, image);

        var fill = state.GetImageRect(image);
        var expectedScale = Math.Max(1200d / imageWidth, 800d / imageHeight);

        fill.Width.Should().BeApproximately(imageWidth * expectedScale, 0.2);
        fill.Height.Should().BeApproximately(imageHeight * expectedScale, 0.2);
        fill.Left.Should().BeApproximately((1200d - fill.Width) / 2d, 0.2);
        fill.Top.Should().BeApproximately((800d - fill.Height) / 2d, 0.2);
        fill.Width.Should().BeGreaterThanOrEqualTo(1199.8);
        fill.Height.Should().BeGreaterThanOrEqualTo(799.8);
        state.GetDisplayScale(image).Should().BeApproximately(expectedScale, 0.001);
    }

    [Fact]
    public void ActualSizeModeUsesIntrinsicPixelsAndEnablesPanWithoutExtraZoom()
    {
        var image = CreateImage(1600, 900);
        var state = new ViewerTransformState();
        state.SetStage(1200, 800, image);
        state.SetScaleMode(ViewerScaleMode.ActualSize, image);

        var actual = state.GetImageRect(image);

        actual.Width.Should().Be(1600);
        actual.Height.Should().Be(900);
        actual.Left.Should().BeApproximately(-200, 0.01);
        actual.Top.Should().BeApproximately(-50, 0.01);
        state.GetDisplayScale(image).Should().Be(1d);
        state.CanPan.Should().BeTrue();
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

        var rendered = state.GetRenderedRect(image);
        rendered.Left.Should().BeLessThanOrEqualTo(0);
        (rendered.Left + rendered.Width).Should().BeGreaterThanOrEqualTo(state.StageWidth);
    }

    [Fact]
    public void PanByIsClampedToOverflowForActualSizeMode()
    {
        var image = CreateImage(1600, 900);
        var state = new ViewerTransformState();
        state.SetStage(1200, 800, image);
        state.SetScaleMode(ViewerScaleMode.ActualSize, image);

        state.PanBy(10_000, 10_000, image);

        state.PanX.Should().BeApproximately(200, 0.01);
        state.PanY.Should().BeApproximately(50, 0.01);
    }

    [Fact]
    public void ResizeRecomputesTheBaseRectAndDropsInvalidPan()
    {
        var image = CreateImage(1600, 900);
        var state = new ViewerTransformState();
        state.SetStage(1200, 800, image);
        state.SetScaleMode(ViewerScaleMode.ActualSize, image);
        state.PanBy(10_000, 10_000, image);

        state.SetStage(1800, 1200, image);

        var actual = state.GetImageRect(image);

        actual.Left.Should().BeApproximately(100, 0.01);
        actual.Top.Should().BeApproximately(150, 0.01);
        state.PanX.Should().Be(0);
        state.PanY.Should().Be(0);
        state.CanPan.Should().BeFalse();
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
