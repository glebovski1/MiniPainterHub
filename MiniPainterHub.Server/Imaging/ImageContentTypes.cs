using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Imaging;

/// <summary>
/// Provides helper methods related to supported image MIME types.
/// </summary>
public static class ImageContentTypes
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/pjpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "image/bmp",
        "image/x-ms-bmp",
        "image/tiff",
        "image/x-tiff"
    };

    /// <summary>
    /// Gets the complete set of allowed image MIME types.
    /// </summary>
    public static IReadOnlyCollection<string> Allowed => AllowedContentTypes;

    /// <summary>
    /// Determines whether the provided MIME type represents a supported image type.
    /// </summary>
    /// <param name="contentType">The MIME type to validate.</param>
    /// <returns><see langword="true"/> if the MIME type is allowed; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowed(string? contentType)
    {
        return contentType is not null && AllowedContentTypes.Contains(contentType);
    }
}
