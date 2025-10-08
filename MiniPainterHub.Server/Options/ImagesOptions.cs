using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Server.Options;

/// <summary>
/// Options controlling server-side image processing.
/// </summary>
public sealed class ImagesOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the image pipeline is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the original upload should also be stored.
    /// </summary>
    public bool KeepOriginal { get; set; }

    /// <summary>
    /// Gets or sets the quality used for lossy encoders.
    /// </summary>
    [Range(10, 100)]
    public int Quality { get; set; } = 80;

    /// <summary>
    /// Gets or sets the preferred output format (webp, jpeg, png).
    /// </summary>
    [Required]
    public string PreferredFormat { get; set; } = "webp";

    /// <summary>
    /// Gets the configuration for the max variant.
    /// </summary>
    public ImageSizeOptions Max { get; set; } = new() { Width = 1920, Height = 1080 };

    /// <summary>
    /// Gets the configuration for the preview variant.
    /// </summary>
    public ImageSizeOptions Preview { get; set; } = new() { Width = 1280, Height = 1280 };

    /// <summary>
    /// Gets the configuration for the thumbnail variant.
    /// </summary>
    public ImageSizeOptions Thumb { get; set; } = new() { Width = 320, Height = 320 };
}

/// <summary>
/// Represents a target size for an image variant.
/// </summary>
public sealed class ImageSizeOptions
{
    /// <summary>
    /// Gets or sets the maximum width of the variant.
    /// </summary>
    [Range(1, 8000)]
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the maximum height of the variant.
    /// </summary>
    [Range(1, 8000)]
    public int Height { get; set; }
}
