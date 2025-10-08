using System;

namespace MiniPainterHub.Server.Services.Models;

/// <summary>
/// Represents a processed image variant payload.
/// </summary>
public sealed class ImageVariant
{
    public ImageVariant(byte[] content, string contentType, string extension, int width, int height)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        if (content.Length == 0)
        {
            throw new ArgumentException("Variant content cannot be empty.", nameof(content));
        }

        ContentType = !string.IsNullOrWhiteSpace(contentType)
            ? contentType
            : throw new ArgumentException("Content type is required.", nameof(contentType));
        Extension = !string.IsNullOrWhiteSpace(extension)
            ? extension.TrimStart('.')
            : throw new ArgumentException("Extension is required.", nameof(extension));
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Gets the encoded image bytes.
    /// </summary>
    public byte[] Content { get; }

    /// <summary>
    /// Gets the MIME type of the payload.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the file extension (without dot).
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// Gets the width of the variant.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the variant.
    /// </summary>
    public int Height { get; }
}
