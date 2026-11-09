using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Images;
using MiniPainterHub.Server.Services.Models;
using ImageMagick;
using MiniPainterHub.Server.Exceptions;
using System;
using System.Text;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class ImageProcessorTests
{
    [Fact]
    public async Task ProcessAsync_AppliesExifOrientation()
    {
        using var stream = await CreateImageAsync(200, 100, image =>
        {
            var profile = new ExifProfile();
            profile.SetValue(ExifTag.Orientation, (ushort)6);
            image.SetProfile(profile);
            image.Orientation = Orientation.RightTop;
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
    public async Task ProcessAsync_ConstrainsPreviewToResponsiveViewingSize()
    {
        using var stream = await CreateImageAsync(1200, 1800);
        var processor = CreateProcessor();

        ImageVariants variants = await processor.ProcessAsync(stream, "image/png", CancellationToken.None);

        variants.Preview.Width.Should().BeLessOrEqualTo(960);
        variants.Preview.Height.Should().BeLessOrEqualTo(960);
        ((double)variants.Preview.Height / variants.Preview.Width).Should().BeApproximately(1.5d, 0.05d);
    }

    [Fact]
    public async Task ProcessAsync_FallsBackToJpeg_WhenPreferredPngWithoutTransparency()
    {
        using var stream = await CreateImageAsync(800, 600, image => image.Colorize(MagickColors.Blue, new Percentage(100))); // opaque
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
            using var pixels = image.GetPixels();
            pixels.SetPixel(10, 10, new byte[] { 255, 0, 0, 0 });
        });
        var processor = CreateProcessor(new ImagesOptions { PreferredFormat = "png" });

        ImageVariants variants = await processor.ProcessAsync(stream, "image/png", CancellationToken.None);

        variants.Max.ContentType.Should().Be("image/png");
        variants.Max.Extension.Should().Be("png");
        using var decoded = new MagickImage(variants.Max.Content);
        decoded.IsOpaque.Should().BeFalse();
    }

    [Theory]
    [InlineData("jpeg", "image/jpeg", MagickFormat.Jpeg)]
    [InlineData("webp", "image/webp", MagickFormat.WebP)]
    public async Task ProcessAsync_ProducesDecodableVariantsAndStripsMetadata(string preferred, string mime, MagickFormat format)
    {
        using var stream = await CreateImageAsync(80, 40, image =>
        {
            var profile = new ExifProfile();
            profile.SetValue(ExifTag.Artist, "Private artist information");
            image.SetProfile(profile);
            image.Comment = "Private comment";
        }, useJpeg: true);
        var result = await CreateProcessor(new ImagesOptions { PreferredFormat = preferred, KeepOriginal = true })
            .ProcessAsync(stream, "image/jpeg", CancellationToken.None);
        foreach (var variant in new[] { result.Max, result.Preview, result.Thumb })
        {
            using var decoded = new MagickImage(variant.Content);
            decoded.Format.Should().Be(format);
            variant.ContentType.Should().Be(mime);
            decoded.Width.Should().Be((uint)variant.Width);
            decoded.Height.Should().Be((uint)variant.Height);
            decoded.GetExifProfile().Should().BeNull();
            decoded.Comment.Should().BeNullOrEmpty();
        }
        result.Original.Should().NotBeNull();
        using var original = new MagickImage(result.Original!.Content);
        original.GetExifProfile().Should().BeNull();
        original.Width.Should().Be(80);
        original.Height.Should().Be(40);
    }

    [Theory]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><image href='file:///etc/passwd'/></svg>")]
    [InlineData("%PDF-1.7 untrusted document")]
    [InlineData("not an image")]
    public async Task ProcessAsync_RejectsNonRasterDataDespiteImageMime(string payload)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        Func<Task> process = () => CreateProcessor().ProcessAsync(stream, "image/png", CancellationToken.None);
        await process.Should().ThrowAsync<UnsupportedImageContentTypeException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsDimensionsBeforeFullDecode()
    {
        using var stream = await CreateImageAsync(100, 50);
        var options = new ImagesOptions { MaxSourceWidth = 80 };
        Func<Task> process = () => CreateProcessor(options).ProcessAsync(stream, "image/png", CancellationToken.None);
        await process.Should().ThrowAsync<UnsupportedImageDimensionsException>();
    }

    [Fact]
    public async Task ProcessAsync_PreservesAnimationFramesInWebP()
    {
        using var frames = new MagickImageCollection();
        frames.Add(new MagickImage(MagickColors.Red, 20, 10) { AnimationDelay = 10 });
        frames.Add(new MagickImage(MagickColors.Blue, 20, 10) { AnimationDelay = 20 });
        using var stream = new MemoryStream();
        await frames.WriteAsync(stream, MagickFormat.Gif);
        stream.Position = 0;
        var result = await CreateProcessor().ProcessAsync(stream, "image/gif", CancellationToken.None);
        using var decoded = new MagickImageCollection(result.Preview.Content);
        decoded.Should().HaveCount(2);
        decoded[0].AnimationDelay.Should().Be(10);
        decoded[1].AnimationDelay.Should().Be(20);
    }

    [Theory]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.WebP)]
    [InlineData(MagickFormat.Gif)]
    [InlineData(MagickFormat.Bmp)]
    [InlineData(MagickFormat.Tiff)]
    public async Task ProcessAsync_AcceptsSupportedRasterFormats(MagickFormat format)
    {
        using var source = new MagickImage(MagickColors.Green, 40, 20);
        using var input = new MemoryStream(source.ToByteArray(format));
        var result = await CreateProcessor().ProcessAsync(input, "image/unknown", CancellationToken.None);
        using var decoded = new MagickImage(result.Thumb.Content);
        decoded.Format.Should().Be(MagickFormat.WebP);
        decoded.Width.Should().Be(320);
        decoded.Height.Should().Be(160);
    }

    [Theory]
    [InlineData("ffd8ff00")]
    [InlineData("89504e470d0a1a0a0000")]
    [InlineData("4749463839610000")]
    public async Task ProcessAsync_RejectsTruncatedRaster(string hex)
    {
        using var input = new MemoryStream(Convert.FromHexString(hex));
        Func<Task> process = () => CreateProcessor().ProcessAsync(input, "image/png", CancellationToken.None);
        await process.Should().ThrowAsync<UnsupportedImageContentTypeException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsAnimationExceedingTotalPixelBudget()
    {
        using var frames = new MagickImageCollection();
        frames.Add(new MagickImage(MagickColors.Red, 800, 800));
        frames.Add(new MagickImage(MagickColors.Blue, 800, 800));
        using var input = new MemoryStream(frames.ToByteArray(MagickFormat.Gif));
        var processor = CreateProcessor(new ImagesOptions { MaxSourceMegapixels = 1 });
        Func<Task> process = () => processor.ProcessAsync(input, "image/gif", CancellationToken.None);
        await process.Should().ThrowAsync<UnsupportedImageDimensionsException>();
    }

    [Fact]
    public async Task ProcessAsync_HonorsCancellation()
    {
        using var input = await CreateImageAsync(20, 10);
        Func<Task> process = () => CreateProcessor().ProcessAsync(input, "image/png", new CancellationToken(true));
        await process.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void NativePolicy_BlocksSvgEvenWhenBypassingApplicationValidation()
    {
        var data = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'/>");
        Action decode = () => { using var image = new MagickImage(data, new MagickReadSettings { Format = MagickFormat.Svg }); };
        decode.Should().Throw<MagickPolicyErrorException>();
        ResourceLimits.Memory.Should().BeLessOrEqualTo(256UL * 1024 * 1024);
        ResourceLimits.ListLength.Should().Be(128);
    }

    private static ImageProcessor CreateProcessor(ImagesOptions? options = null)
    {
        options ??= new ImagesOptions();
        return new ImageProcessor(Microsoft.Extensions.Options.Options.Create(options), NullLogger<ImageProcessor>.Instance);
    }

    private static async Task<MemoryStream> CreateImageAsync(int width, int height, System.Action<MagickImage>? configure = null, bool useJpeg = false)
    {
        using var image = new MagickImage(MagickColors.White, (uint)width, (uint)height);
        image.HasAlpha = true;
        configure?.Invoke(image);

        var ms = new MemoryStream();
        if (useJpeg)
        {
            await image.WriteAsync(ms, MagickFormat.Jpeg);
        }
        else
        {
            await image.WriteAsync(ms, MagickFormat.Png);
        }
        ms.Position = 0;
        return ms;
    }
}
