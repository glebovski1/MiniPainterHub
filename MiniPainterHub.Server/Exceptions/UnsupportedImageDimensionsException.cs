using System;

namespace MiniPainterHub.Server.Exceptions;

public sealed class UnsupportedImageDimensionsException : Exception
{
    public UnsupportedImageDimensionsException(string fileName, int width, int height, int maxWidth, int maxHeight, int maxMegapixels)
        : base($"Image '{fileName}' is {width}x{height}. Images must be at most {maxWidth}x{maxHeight} and {maxMegapixels} megapixels.")
    {
        FileName = fileName;
        Width = width;
        Height = height;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        MaxMegapixels = maxMegapixels;
    }

    public string FileName { get; }

    public int Width { get; }

    public int Height { get; }

    public int MaxWidth { get; }

    public int MaxHeight { get; }

    public int MaxMegapixels { get; }
}
