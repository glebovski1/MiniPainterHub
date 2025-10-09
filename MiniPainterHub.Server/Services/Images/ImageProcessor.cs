using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MiniPainterHub.Server.Services.Images;

/// <summary>
/// ImageSharp-based implementation that generates application-specific variants.
/// </summary>
public sealed class ImageProcessor : IImageProcessor
{
    private readonly ImagesOptions _options;
    private readonly ILogger<ImageProcessor> _logger;

    public ImageProcessor(IOptions<ImagesOptions> options, ILogger<ImageProcessor> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ImageVariants> ProcessAsync(Stream input, string? contentType, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var start = DateTimeOffset.UtcNow;
        await using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        try
        {
            using var image = await Image.LoadAsync<Rgba32>(buffer, ct);
            ApplyOrientation(image);
            NormalizeMetadata(image.Metadata);

            var hasTransparency = ContainsTransparency(image);
            var encoderInfo = SelectEncoder(hasTransparency);

            var maxVariant = await CreateVariantAsync(image, _options.Max, encoderInfo, ct);
            var previewVariant = await CreateVariantAsync(image, _options.Preview, encoderInfo, ct);
            var thumbVariant = await CreateVariantAsync(image, _options.Thumb, encoderInfo, ct);

            ImageVariant? originalVariant = null;
            if (_options.KeepOriginal)
            {
                var decodedFormat = image.Metadata.DecodedImageFormat;
                if (decodedFormat is not null)
                {
                    using var originalStream = new MemoryStream();
                    await image.SaveAsync(originalStream, decodedFormat, ct);
                    var extension = decodedFormat.FileExtensions.FirstOrDefault() ?? encoderInfo.Extension;
                    var mime = decodedFormat.DefaultMimeType ?? encoderInfo.ContentType;
                    originalVariant = new ImageVariant(originalStream.ToArray(), mime, extension, image.Width, image.Height);
                }
            }

            _logger.LogInformation("Processed image in {Elapsed} ms ({Width}x{Height})", (DateTimeOffset.UtcNow - start).TotalMilliseconds, image.Width, image.Height);

            return new ImageVariants(maxVariant, previewVariant, thumbVariant, originalVariant);
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.LogWarning(ex, "Unsupported image format declared as {ContentType}", contentType);
            throw;
        }
    }

    private static void ApplyOrientation(Image image)
    {
        if (image.Metadata.ExifProfile is null)
        {
            return;
        }

        image.Mutate(x => x.AutoOrient());
    }

    private static void NormalizeMetadata(ImageMetadata metadata)
    {
        metadata.ExifProfile = null;
        metadata.IptcProfile = null;
        metadata.XmpProfile = null;
        metadata.IccProfile = null;
    }

    private async Task<ImageVariant> CreateVariantAsync(Image<Rgba32> source, ImageSizeOptions target, EncoderInfo encoderInfo, CancellationToken ct)
    {
        using var clone = source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(target.Width, target.Height),
            Sampler = KnownResamplers.Lanczos3
        }));

        NormalizeMetadata(clone.Metadata);

        using var ms = new MemoryStream();
        await clone.SaveAsync(ms, encoderInfo.Encoder, ct);

        return new ImageVariant(ms.ToArray(), encoderInfo.ContentType, encoderInfo.Extension, clone.Width, clone.Height);
    }

    private EncoderInfo SelectEncoder(bool hasTransparency)
    {
        var preferred = _options.PreferredFormat?.Trim().ToLowerInvariant();
        return preferred switch
        {
            "webp" => new EncoderInfo(new WebpEncoder { Quality = _options.Quality }, "webp", "image/webp"),
            "png" when hasTransparency => new EncoderInfo(new PngEncoder { ColorType = PngColorType.RgbWithAlpha }, "png", "image/png"),
            "jpeg" or "jpg" => new EncoderInfo(new JpegEncoder { Quality = _options.Quality }, "jpg", "image/jpeg"),
            "png" => new EncoderInfo(new JpegEncoder { Quality = _options.Quality }, "jpg", "image/jpeg"),
            _ when hasTransparency => new EncoderInfo(new PngEncoder { ColorType = PngColorType.RgbWithAlpha }, "png", "image/png"),
            _ => new EncoderInfo(new JpegEncoder { Quality = _options.Quality }, "jpg", "image/jpeg")
        };
    }

    private static bool ContainsTransparency(Image<Rgba32> image)
    {
        var frame = image.Frames.RootFrame;

        for (var y = 0; y < frame.Height; y++)
        {
            var span = frame.GetPixelRowSpan(y);

            foreach (var pixel in span)
            {
                if (pixel.A < 255)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record EncoderInfo(IImageEncoder Encoder, string Extension, string ContentType);
}
