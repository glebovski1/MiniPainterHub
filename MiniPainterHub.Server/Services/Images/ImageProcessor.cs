using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Imaging;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Services.Images;

/// <summary>Generates application-specific raster variants with Magick.NET.</summary>
public sealed class ImageProcessor : IImageProcessor
{
    private readonly ImagesOptions _options;
    private readonly ILogger<ImageProcessor> _logger;

    public ImageProcessor(IOptions<ImagesOptions> options, ILogger<ImageProcessor> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImageVariants> ProcessAsync(Stream input, string? contentType, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var start = DateTimeOffset.UtcNow;
        using var images = await RasterImageDecoder.ReadAsync(input, _options, "uploaded image", contentType, ct);
        var originalFormat = images[0].Format;
        foreach (var frame in images)
        {
            frame.AutoOrient();
            frame.Strip();
        }

        var encoder = SelectEncoder(images.Any(frame => !frame.IsOpaque));
        var max = await CreateVariantAsync(images, _options.Max, encoder, ct);
        var preview = await CreateVariantAsync(images, _options.Preview, encoder, ct);
        var thumb = await CreateVariantAsync(images, _options.Thumb, encoder, ct);
        ImageVariant? original = null;
        if (_options.KeepOriginal)
        {
            var originalEncoder = DescribeFormat(originalFormat);
            using var output = new MemoryStream();
            await images.WriteAsync(output, originalEncoder.Format, ct);
            original = new ImageVariant(output.ToArray(), originalEncoder.ContentType, originalEncoder.Extension,
                checked((int)images[0].Width), checked((int)images[0].Height));
        }

        _logger.LogInformation("Processed image in {Elapsed} ms ({Width}x{Height})",
            (DateTimeOffset.UtcNow - start).TotalMilliseconds, images[0].Width, images[0].Height);
        return new ImageVariants(max, preview, thumb, original);
    }

    private async Task<ImageVariant> CreateVariantAsync(MagickImageCollection source, ImageSizeOptions target,
        EncoderInfo encoder, CancellationToken ct)
    {
        using var clone = source.Clone();
        foreach (var frame in clone)
        {
            ct.ThrowIfCancellationRequested();
            frame.Resize((uint)target.Width, (uint)target.Height, FilterType.Lanczos);
            frame.ResetPage();
            frame.Strip();
            frame.Quality = (uint)_options.Quality;
        }

        using var output = new MemoryStream();
        // JPEG has no animation; retain the first displayed frame as before.
        if (encoder.Format == MagickFormat.Jpeg)
        {
            await clone[0].WriteAsync(output, encoder.Format, ct);
        }
        else
        {
            await clone.WriteAsync(output, encoder.Format, ct);
        }

        return new ImageVariant(output.ToArray(), encoder.ContentType, encoder.Extension,
            checked((int)clone[0].Width), checked((int)clone[0].Height));
    }

    private EncoderInfo SelectEncoder(bool transparent) => (_options.PreferredFormat?.Trim().ToLowerInvariant()) switch
    {
        "webp" => DescribeFormat(MagickFormat.WebP),
        "png" when transparent => DescribeFormat(MagickFormat.Png),
        "png" or "jpeg" or "jpg" => DescribeFormat(MagickFormat.Jpeg),
        _ when transparent => DescribeFormat(MagickFormat.Png),
        _ => DescribeFormat(MagickFormat.Jpeg)
    };

    private static EncoderInfo DescribeFormat(MagickFormat format) => format switch
    {
        MagickFormat.WebP => new(format, "webp", "image/webp"),
        MagickFormat.Gif => new(format, "gif", "image/gif"),
        MagickFormat.Tiff => new(format, "tiff", "image/tiff"),
        MagickFormat.Bmp or MagickFormat.Bmp2 or MagickFormat.Bmp3 => new(MagickFormat.Bmp, "bmp", "image/bmp"),
        MagickFormat.Jpeg => new(format, "jpg", "image/jpeg"),
        _ => new(MagickFormat.Png, "png", "image/png")
    };

    private sealed record EncoderInfo(MagickFormat Format, string Extension, string ContentType);
}
