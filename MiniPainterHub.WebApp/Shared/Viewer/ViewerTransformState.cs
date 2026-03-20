using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.WebApp.Shared.Viewer;

public enum ViewerScaleMode
{
    Fit,
    Fill,
    ActualSize
}

internal sealed class ViewerTransformState
{
    private const double ImageWidthFallback = 1600d;
    private const double ImageHeightFallback = 1000d;
    private const double PanThreshold = 0.001d;

    public const double MinZoom = 1d;
    public const double MaxZoom = 5d;
    public const double ZoomStep = 0.25d;

    private ViewerStageSnapshot _stage = new(960d, 640d);

    public ViewerScaleMode ScaleMode { get; private set; } = ViewerScaleMode.Fit;
    public double Zoom { get; private set; } = MinZoom;
    public double PanX { get; private set; }
    public double PanY { get; private set; }
    public double StageWidth => _stage.Width;
    public double StageHeight => _stage.Height;
    public bool CanPan { get; private set; }

    public void SetStage(double width, double height, PostViewerImageDto? image)
    {
        if (width <= 0d || height <= 0d)
        {
            return;
        }

        _stage = new ViewerStageSnapshot(width, height);
        ClampPan(image);
    }

    public void Reset(PostViewerImageDto? image)
    {
        ScaleMode = ViewerScaleMode.Fit;
        Zoom = MinZoom;
        PanX = 0d;
        PanY = 0d;
        ClampPan(image);
    }

    public void SetScaleMode(ViewerScaleMode nextMode, PostViewerImageDto? image)
    {
        ScaleMode = nextMode;
        Zoom = MinZoom;
        PanX = 0d;
        PanY = 0d;
        ClampPan(image);
    }

    public void SetZoom(double nextZoom, PostViewerImageDto? image)
    {
        Zoom = Math.Clamp(nextZoom, MinZoom, MaxZoom);
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

        var baseRect = GetImageRect(image);
        var scaledWidth = baseRect.Width * Zoom;
        var scaledHeight = baseRect.Height * Zoom;

        PanX = -(((double)normalizedX - 0.5d) * scaledWidth);
        PanY = -(((double)normalizedY - 0.5d) * scaledHeight);
        ClampPan(image);
    }

    public double GetDisplayScale(PostViewerImageDto? image)
    {
        var (imageWidth, _) = GetImageSize(image);
        if (imageWidth <= 0d)
        {
            return 0d;
        }

        var baseRect = GetImageRect(image);
        return (baseRect.Width * Zoom) / imageWidth;
    }

    public ViewerImageRectSnapshot GetImageRect(PostViewerImageDto? image)
    {
        if (image is null)
        {
            return new ViewerImageRectSnapshot(0d, 0d, 0d, 0d);
        }

        var (imageWidth, imageHeight) = GetImageSize(image);
        var scale = ScaleMode switch
        {
            ViewerScaleMode.Fill => Math.Max(_stage.Width / imageWidth, _stage.Height / imageHeight),
            ViewerScaleMode.ActualSize => 1d,
            _ => Math.Min(_stage.Width / imageWidth, _stage.Height / imageHeight)
        };

        var width = imageWidth * scale;
        var height = imageHeight * scale;

        return new ViewerImageRectSnapshot(
            Left: (_stage.Width - width) / 2d,
            Top: (_stage.Height - height) / 2d,
            Width: width,
            Height: height);
    }

    public ViewerImageRectSnapshot GetRenderedRect(PostViewerImageDto? image)
    {
        if (image is null)
        {
            return new ViewerImageRectSnapshot(0d, 0d, 0d, 0d);
        }

        var baseRect = GetImageRect(image);
        var width = baseRect.Width * Zoom;
        var height = baseRect.Height * Zoom;

        return new ViewerImageRectSnapshot(
            Left: ((_stage.Width - width) / 2d) + PanX,
            Top: ((_stage.Height - height) / 2d) + PanY,
            Width: width,
            Height: height);
    }

    private void ClampPan(PostViewerImageDto? image)
    {
        if (image is null)
        {
            PanX = 0d;
            PanY = 0d;
            CanPan = false;
            return;
        }

        var baseRect = GetImageRect(image);
        var scaledWidth = baseRect.Width * Zoom;
        var scaledHeight = baseRect.Height * Zoom;
        var maxPanX = Math.Max(0d, (scaledWidth - _stage.Width) / 2d);
        var maxPanY = Math.Max(0d, (scaledHeight - _stage.Height) / 2d);

        PanX = Math.Clamp(PanX, -maxPanX, maxPanX);
        PanY = Math.Clamp(PanY, -maxPanY, maxPanY);
        CanPan = maxPanX > PanThreshold || maxPanY > PanThreshold;
    }

    private static (double Width, double Height) GetImageSize(PostViewerImageDto? image)
    {
        if (image is null)
        {
            return (0d, 0d);
        }

        var width = image.Width.GetValueOrDefault() > 0 ? image.Width!.Value : ImageWidthFallback;
        var height = image.Height.GetValueOrDefault() > 0 ? image.Height!.Value : ImageHeightFallback;
        return (width, height);
    }
}

internal readonly record struct ViewerStageSnapshot(double Width, double Height);

internal readonly record struct ViewerImageRectSnapshot(double Left, double Top, double Width, double Height);
