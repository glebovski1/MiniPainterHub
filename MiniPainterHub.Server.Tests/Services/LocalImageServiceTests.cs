using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Models;
using Moq;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class LocalImageServiceTests
{
    [Fact]
    public void Ctor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.SetupGet(e => e.WebRootPath).Returns("C:\\temp");
        var config = new ConfigurationBuilder().Build();

        var act = () => new LocalImageService(envMock.Object, config, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task UploadAsync_SavesFileAndReturnsRelativeUrl()
    {
        await RunWithServiceAsync(async (service, basePath) =>
        {
            await using var content = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var url = await service.UploadAsync(content, "my file.png");

            url.Should().Be("/images/my%20file.png");
            File.Exists(Path.Combine(basePath, "my file.png")).Should().BeTrue();
        });
    }

    [Fact]
    public async Task DownloadAsync_WhenFileIsMissing_ThrowsNotFoundException()
    {
        await RunWithServiceAsync(async (service, _) =>
        {
            var act = async () => await service.DownloadAsync("missing.png");

            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("Image 'missing.png' not found.");
        });
    }

    [Fact]
    public async Task DownloadAsync_WhenFileExists_ReturnsReadableStream()
    {
        await RunWithServiceAsync(async (service, basePath) =>
        {
            var filePath = Path.Combine(basePath, "image.png");
            await File.WriteAllBytesAsync(filePath, new byte[] { 9, 8, 7 });

            await using var stream = await service.DownloadAsync("image.png");
            using var reader = new MemoryStream();
            await stream.CopyToAsync(reader);

            reader.ToArray().Should().Equal(new byte[] { 9, 8, 7 });
        });
    }

    [Fact]
    public async Task DeleteAsync_WhenFileExists_RemovesFile()
    {
        await RunWithServiceAsync(async (service, basePath) =>
        {
            var filePath = Path.Combine(basePath, "delete-me.png");
            await File.WriteAllBytesAsync(filePath, new byte[] { 1 });
            File.Exists(filePath).Should().BeTrue();

            await service.DeleteAsync("delete-me.png");

            File.Exists(filePath).Should().BeFalse();
        });
    }

    [Fact]
    public async Task DeleteAsync_WhenFileMissing_DoesNotThrow()
    {
        await RunWithServiceAsync(async (service, _) =>
        {
            var act = async () => await service.DeleteAsync("nope.png");
            await act.Should().NotThrowAsync();
        });
    }

    [Fact]
    public async Task SaveAsync_WhenVariantsAreNull_ThrowsArgumentNullException()
    {
        await RunWithServiceAsync(async (service, _) =>
        {
            var act = async () => await service.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), null!, default);
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("variants");
        });
    }

    [Fact]
    public async Task SaveAsync_WhenKeepOriginalDisabled_WritesThreeFiles()
    {
        await RunWithServiceAsync(async (service, basePath) =>
        {
            var postId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var imageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var result = await service.SaveAsync(postId, imageId, CreateVariants(includeOriginal: true), default);

            result.OriginalUrl.Should().BeNull();
            result.MaxUrl.Should().Contain("_max.");
            result.PreviewUrl.Should().Contain("_preview.");
            result.ThumbUrl.Should().Contain("_thumb.");

            var folder = Path.Combine(basePath, postId.ToString("D"));
            Directory.GetFiles(folder).Should().HaveCount(3);
        });
    }

    [Fact]
    public async Task SaveAsync_WhenKeepOriginalEnabled_WritesOriginalFile()
    {
        await RunWithServiceAsync(async (service, basePath) =>
        {
            var postId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var imageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var result = await service.SaveAsync(postId, imageId, CreateVariants(includeOriginal: true), default);

            result.OriginalUrl.Should().Contain("_original.");

            var folder = Path.Combine(basePath, postId.ToString("D"));
            Directory.GetFiles(folder).Should().HaveCount(4);
        }, new ImagesOptions { KeepOriginal = true });
    }

    private static ImageVariants CreateVariants(bool includeOriginal)
    {
        var max = new ImageVariant(new byte[] { 1, 2, 3 }, "image/webp", "webp", 1200, 800);
        var preview = new ImageVariant(new byte[] { 4, 5, 6 }, "image/webp", "webp", 640, 480);
        var thumb = new ImageVariant(new byte[] { 7, 8, 9 }, "image/webp", "webp", 320, 240);
        var original = includeOriginal ? new ImageVariant(new byte[] { 10, 11, 12 }, "image/jpeg", "jpg", 2000, 1500) : null;
        return new ImageVariants(max, preview, thumb, original);
    }

    private static async Task RunWithServiceAsync(
        Func<LocalImageService, string, Task> assertion,
        ImagesOptions? options = null)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var envMock = new Mock<IWebHostEnvironment>();
            envMock.SetupGet(e => e.WebRootPath).Returns(tempRoot);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ImageStorage:LocalPath"] = tempRoot,
                    ["ImageStorage:RequestPath"] = "/images"
                })
                .Build();

            var service = new LocalImageService(
                envMock.Object,
                config,
                Microsoft.Extensions.Options.Options.Create(options ?? new ImagesOptions()));

            await assertion(service, tempRoot);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
