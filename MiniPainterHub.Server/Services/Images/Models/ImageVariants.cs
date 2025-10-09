namespace MiniPainterHub.Server.Services.Images.Models;

/// <summary>
/// Contains the different processed image variants.
/// </summary>
public sealed class ImageVariants
{
    public ImageVariants(ImageVariant max, ImageVariant preview, ImageVariant thumb, ImageVariant? original = null)
    {
        Max = max;
        Preview = preview;
        Thumb = thumb;
        Original = original;
    }

    /// <summary>
    /// Gets the max-sized variant.
    /// </summary>
    public ImageVariant Max { get; }

    /// <summary>
    /// Gets the preview variant.
    /// </summary>
    public ImageVariant Preview { get; }

    /// <summary>
    /// Gets the thumbnail variant.
    /// </summary>
    public ImageVariant Thumb { get; }

    /// <summary>
    /// Gets the optional original upload.
    /// </summary>
    public ImageVariant? Original { get; }
}
