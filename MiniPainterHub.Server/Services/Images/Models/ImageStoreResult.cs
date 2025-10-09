namespace MiniPainterHub.Server.Services.Images.Models;

/// <summary>
/// Result returned from persisting processed image variants.
/// </summary>
public sealed class ImageStoreResult
{
    public ImageStoreResult(string maxUrl, string previewUrl, string thumbUrl, string? originalUrl = null)
    {
        MaxUrl = maxUrl;
        PreviewUrl = previewUrl;
        ThumbUrl = thumbUrl;
        OriginalUrl = originalUrl;
    }

    /// <summary>
    /// Gets the URL for the max variant.
    /// </summary>
    public string MaxUrl { get; }

    /// <summary>
    /// Gets the URL for the preview variant.
    /// </summary>
    public string PreviewUrl { get; }

    /// <summary>
    /// Gets the URL for the thumbnail variant.
    /// </summary>
    public string ThumbUrl { get; }

    /// <summary>
    /// Gets the URL to the original upload when preserved.
    /// </summary>
    public string? OriginalUrl { get; }
}
