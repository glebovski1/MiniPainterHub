using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.WebApp.Shared.Viewer;

internal sealed class ViewerTransformState
{
    public const double MinZoom = 1d;
    public const double MaxZoom = 5d;
    public const double ZoomStep = 0.25d;

    private ViewerStageSnapshot _stage = new(960d, 640d);

    public double Zoom { get; private set; } = MinZoom;
    public double PanX { get; private set; }
    public double PanY { get; private set; }

    public double StageWidth => _stage.Width;
    public double StageHeight => _stage.Height;
    public bool CanPan => Zoom > (MinZoom + 0.001d);

    public void SetStage(double width, double height, PostViewerImageDto? image)
    {
        if (width <= 0d || height <= 0d)
        {
            return;
        }

        _stage = new ViewerStageSnapshot(width, height);
        ClampPan(image);
    }

    public void Reset()
    {
        Zoom = MinZoom;
        PanX = 0d;
        PanY = 0d;
    }

    public void SetZoom(double nextZoom, PostViewerImageDto? image)
    {
        Zoom = Math.Clamp(nextZoom, MinZoom, MaxZoom);
        if (Zoom <= MinZoom + 0.001d)
        {
            Reset();
            return;
        }

        ClampPan(image);
    }

    public void PanBy(double deltaX, double deltaY, PostViewerImageDto? image)
    {
        PanX += deltaX;
        PanY += deltaY;
        ClampPan(image);
    }

    public void CenterOn(decimal normalizedX, decimal normalizedY, bool promoteZoom, PostViewerImageDto? image)
    {
        if (image is null)
        {
            return;
        }

        var targetZoom = promoteZoom && Zoom < 1.2d ? 1.2d : Zoom;
        Zoom = Math.Clamp(targetZoom, MinZoom, MaxZoom);

        var fit = GetFitRect(image);
        var deltaX = ((double)normalizedX - 0.5d) * fit.Width * Zoom;
        var deltaY = ((double)normalizedY - 0.5d) * fit.Height * Zoom;

        PanX = -deltaX;
        PanY = -deltaY;
        ClampPan(image);
    }

    public ViewerFitRectSnapshot GetFitRect(PostViewerImageDto? image)
    {
        if (image is null)
        {
            return new ViewerFitRectSnapshot(0d, 0d, 0d, 0d);
        }

        var imageWidth = image.Width.GetValueOrDefault() > 0 ? image.Width!.Value : 1600d;
        var imageHeight = image.Height.GetValueOrDefault() > 0 ? image.Height!.Value : 1000d;
        var scale = Math.Min(_stage.Width / imageWidth, _stage.Height / imageHeight);
        var width = imageWidth * scale;
        var height = imageHeight * scale;

        return new ViewerFitRectSnapshot(
            Left: (_stage.Width - width) / 2d,
            Top: (_stage.Height - height) / 2d,
            Width: width,
            Height: height);
    }

    private void ClampPan(PostViewerImageDto? image)
    {
        if (image is null)
        {
            PanX = 0d;
            PanY = 0d;
            return;
        }

        var fit = GetFitRect(image);
        var scaledWidth = fit.Width * Zoom;
        var scaledHeight = fit.Height * Zoom;
        var maxPanX = Math.Max(0d, (scaledWidth - _stage.Width) / 2d);
        var maxPanY = Math.Max(0d, (scaledHeight - _stage.Height) / 2d);

        PanX = Math.Clamp(PanX, -maxPanX, maxPanX);
        PanY = Math.Clamp(PanY, -maxPanY, maxPanY);
    }
}

internal readonly record struct ViewerStageSnapshot(double Width, double Height);

internal readonly record struct ViewerFitRectSnapshot(double Left, double Top, double Width, double Height);
