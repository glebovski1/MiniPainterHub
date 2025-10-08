using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Images;
using MiniPainterHub.Server.Services.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class ImageProcessorTests
{
    [Fact]
    public async Task ProcessAsync_AppliesExifOrientation()
    {
        using var stream = await CreateImageAsync(200, 100, image =>
        {
            image.Metadata.ExifProfile ??= new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        }, useJpeg: true);

        var processor = CreateProcessor();

        ImageVariants variants = await processor.ProcessAsync(stream, "image/jpeg", CancellationToken.None);

        variants.Max.Width.Should().BeLessThan(variants.Max.Height);
    }

    [Fact]
    public async Task ProcessAsync_ResizesWideImageMaintainingAspect()
    {
        using var stream = await CreateImageAsync(4000, 1000);
        var processor = CreateProcessor();

        ImageVariants variants = await processor.ProcessAsync(stream, "image/jpeg", CancellationToken.None);

        variants.Max.Width.Should().BeLessOrEqualTo(1920);
        variants.Max.Height.Should().BeLessOrEqualTo(1080);
        ((double)variants.Max.Width / variants.Max.Height).Should().BeApproximately(4d, 0.05d);
    }

    [Fact]
    public async Task ProcessAsync_FallsBackToJpeg_WhenPreferredPngWithoutTransparency()
    {
        using var stream = await CreateImageAsync(800, 600, image => image.Mutate(ctx => ctx.BackgroundColor(Color.Blue))); // opaque
        var processor = CreateProcessor(new ImagesOptions { PreferredFormat = "png" });

        ImageVariants variants = await processor.ProcessAsync(stream, "image/png", CancellationToken.None);

        variants.Max.ContentType.Should().Be("image/jpeg");
        variants.Max.Extension.Should().Be("jpg");
    }

    [Fact]
    public async Task ProcessAsync_KeepsPng_WhenTransparencyDetected()
    {
        using var stream = await CreateImageAsync(256, 256, image =>
        {
            image[10, 10] = new Rgba32(255, 0, 0, 0);
        });
        var processor = CreateProcessor(new ImagesOptions { PreferredFormat = "png" });

        ImageVariants variants = await processor.ProcessAsync(stream, "image/png", CancellationToken.None);

        variants.Max.ContentType.Should().Be("image/png");
        variants.Max.Extension.Should().Be("png");
    }

    private static ImageProcessor CreateProcessor(ImagesOptions? options = null)
    {
        options ??= new ImagesOptions();
        return new ImageProcessor(Options.Create(options), NullLogger<ImageProcessor>.Instance);
    }

    private static async Task<MemoryStream> CreateImageAsync(int width, int height, System.Action<Image<Rgba32>>? configure = null, bool useJpeg = false)
    {
        var image = new Image<Rgba32>(width, height);
        configure?.Invoke(image);

        var ms = new MemoryStream();
        if (useJpeg)
        {
            await image.SaveAsJpegAsync(ms);
        }
        else
        {
            await image.SaveAsPngAsync(ms);
        }
        ms.Position = 0;
        return ms;
    }
}
