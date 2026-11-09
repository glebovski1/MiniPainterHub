using ImageMagick;
using ImageMagick.Configuration;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPainterHub.Server.Imaging;

internal static class RasterImageDecoder
{
    private static readonly Lazy<bool> Initialization = new(() =>
    {
        var configuration = ConfigurationFiles.Default;
        // Read from the published application; fail startup if the policy is missing.
        configuration.Policy.Data = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Imaging", "policy.xml"));
        MagickNET.Initialize(configuration);
        return true;
    });

    internal static void Initialize() => _ = Initialization.Value;

    internal static async Task<MagickImageCollection> ReadAsync(
        Stream input, ImagesOptions options, string fileName, string? contentType, CancellationToken ct)
    {
        Initialize();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        ct.ThrowIfCancellationRequested();
        var settings = new MagickReadSettings { Format = DetectFormat(bytes, fileName, contentType) };
        var images = new MagickImageCollection();
        try
        {
            images.Ping(bytes, settings);
            ValidateDimensions(images, options, fileName);
            ct.ThrowIfCancellationRequested();
            images.Read(bytes, settings);
            ct.ThrowIfCancellationRequested();
            if (images.Count > 1)
            {
                images.Coalesce();
            }

            ct.ThrowIfCancellationRequested();
            return images;
        }
        catch (MagickException)
        {
            images.Dispose();
            throw new UnsupportedImageContentTypeException(fileName, contentType ?? string.Empty);
        }
        catch
        {
            images.Dispose();
            throw;
        }
    }

    internal static async Task ValidateAsync(Stream input, ImagesOptions options,
        string fileName, string? contentType, CancellationToken ct)
    {
        Initialize();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        ct.ThrowIfCancellationRequested();
        var settings = new MagickReadSettings { Format = DetectFormat(bytes, fileName, contentType) };
        try
        {
            using var images = new MagickImageCollection();
            images.Ping(bytes, settings);
            ValidateDimensions(images, options, fileName);
        }
        catch (MagickException)
        {
            throw new UnsupportedImageContentTypeException(fileName, contentType ?? string.Empty);
        }
    }

    private static MagickFormat DetectFormat(ReadOnlySpan<byte> bytes, string fileName, string? contentType)
    {
        // Never auto-detect the broader ImageMagick formats from untrusted input.
        // Pin the decoder to a supported raster signature, independently of MIME/file name.
        if (bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff })) return MagickFormat.Jpeg;
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return MagickFormat.Png;
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return MagickFormat.Gif;
        if (bytes.StartsWith("BM"u8)) return MagickFormat.Bmp;
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return MagickFormat.WebP;
        if (bytes.StartsWith(new byte[] { 73, 73, 42, 0 })
            || bytes.StartsWith(new byte[] { 77, 77, 0, 42 })
            || bytes.StartsWith(new byte[] { 73, 73, 43, 0 })
            || bytes.StartsWith(new byte[] { 77, 77, 0, 43 })) return MagickFormat.Tiff;
        throw new UnsupportedImageContentTypeException(fileName, contentType ?? string.Empty);
    }

    private static void ValidateDimensions(MagickImageCollection images, ImagesOptions options, string fileName)
    {
        long totalPixels = 0;
        foreach (var frame in images)
        {
            var width = Math.Max(frame.Width, frame.Page.Width);
            var height = Math.Max(frame.Height, frame.Page.Height);
            if (width > options.MaxSourceWidth || height > options.MaxSourceHeight
                || (totalPixels += (long)width * height) > options.MaxSourceMegapixels * 1_000_000L)
            {
                throw new UnsupportedImageDimensionsException(fileName, (int)Math.Min(width, int.MaxValue), (int)Math.Min(height, int.MaxValue),
                    options.MaxSourceWidth, options.MaxSourceHeight, options.MaxSourceMegapixels);
            }
        }
    }
}
