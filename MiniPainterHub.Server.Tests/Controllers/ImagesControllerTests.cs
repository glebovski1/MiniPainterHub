using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniPainterHub.Server.Controllers;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class ImagesControllerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/uploads/images/../secret.png")]
    [InlineData("C:/temp/image.png")]
    [InlineData("/not-uploads/image.png")]
    public async Task GetThumbnail_WhenUrlIsInvalid_ReturnsBadRequest(string? url)
    {
        var controller = CreateController(new RecordingImageService(), new RecordingImageProcessor());

        var result = await controller.GetThumbnail(url, CancellationToken.None);

        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task GetThumbnail_WhenCacheIsMissing_ProcessesAndCachesThumb()
    {
        var imageService = new RecordingImageService();
        imageService.Files["seed-post.png"] = new byte[] { 1, 2, 3 };
        var imageProcessor = new RecordingImageProcessor();
        var controller = CreateController(imageService, imageProcessor);

        var result = await controller.GetThumbnail("/uploads/images/seed-post.png", CancellationToken.None);

        result.Should().BeOfType<FileStreamResult>()
            .Which.ContentType.Should().Be("image/webp");
        imageProcessor.ProcessCount.Should().Be(1);
        imageService.UploadedFileName.Should().StartWith("thumbnail-cache-");
        imageService.UploadedBytes.Should().Equal(new byte[] { 9, 8, 7 });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/uploads/images/../secret.png")]
    [InlineData("C:/temp/image.png")]
    [InlineData("/not-uploads/image.png")]
    public async Task GetPreview_WhenUrlIsInvalid_ReturnsBadRequest(string? url)
    {
        var controller = CreateController(new RecordingImageService(), new RecordingImageProcessor());

        var result = await controller.GetPreview(url, CancellationToken.None);

        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task GetPreview_WhenCacheIsMissing_ProcessesAndCachesPreview()
    {
        var imageService = new RecordingImageService();
        imageService.Files["seed-post.png"] = new byte[] { 1, 2, 3 };
        var imageProcessor = new RecordingImageProcessor();
        var controller = CreateController(imageService, imageProcessor);

        var result = await controller.GetPreview("/uploads/images/seed-post.png", CancellationToken.None);

        result.Should().BeOfType<FileStreamResult>()
            .Which.ContentType.Should().Be("image/webp");
        imageProcessor.ProcessCount.Should().Be(1);
        imageService.UploadedFileName.Should().StartWith("preview-cache-");
        imageService.UploadedBytes.Should().Equal(new byte[] { 2 });
    }

    private static ImagesController CreateController(RecordingImageService imageService, RecordingImageProcessor imageProcessor) =>
        new(imageService, imageProcessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private sealed class RecordingImageService : IImageService
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string? UploadedFileName { get; private set; }
        public byte[] UploadedBytes { get; private set; } = Array.Empty<byte>();

        public Task DeleteAsync(string fileName) => Task.CompletedTask;

        public Task<Stream> DownloadAsync(string fileName)
        {
            if (!Files.TryGetValue(fileName, out var bytes))
            {
                throw new NotFoundException(fileName);
            }

            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            await using var buffer = new MemoryStream();
            await fileStream.CopyToAsync(buffer);
            UploadedFileName = fileName;
            UploadedBytes = buffer.ToArray();
            Files[fileName] = UploadedBytes;
            return "/uploads/images/" + fileName;
        }
    }

    private sealed class RecordingImageProcessor : IImageProcessor
    {
        public int ProcessCount { get; private set; }

        public Task<ImageVariants> ProcessAsync(Stream input, string? contentType, CancellationToken ct)
        {
            ProcessCount++;
            var max = new ImageVariant(new byte[] { 1 }, "image/webp", "webp", 1, 1);
            var preview = new ImageVariant(new byte[] { 2 }, "image/webp", "webp", 1, 1);
            var thumb = new ImageVariant(new byte[] { 9, 8, 7 }, "image/webp", "webp", 1, 1);
            return Task.FromResult(new ImageVariants(max, preview, thumb));
        }
    }
}
