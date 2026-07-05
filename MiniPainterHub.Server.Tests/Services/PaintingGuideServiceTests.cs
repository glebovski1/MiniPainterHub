using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Tests.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class PaintingGuideServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenGuideHasStepsAndPhoto_PersistsGuideAndStepPhoto()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "artist");
        user.Profile = TestData.CreateProfile(user.Id, "Display Painter");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var imageService = new StubImageService();
        var service = new PaintingGuideService(context, imageService);
        var dto = CreateGuideDto();
        var photo = CreateFormFile(CreateJpegBytes(), "basecoat.jpg", "image/jpeg");

        var result = await service.CreateAsync(
            user.Id,
            dto,
            new Dictionary<int, IFormFile> { [0] = photo },
            CancellationToken.None);

        result.Title.Should().Be(dto.Title);
        result.AuthorName.Should().Be("Display Painter");
        result.Steps.Should().HaveCount(2);
        result.Steps[0].ImageUrl.Should().StartWith("https://images.test/guide-");
        imageService.UploadedFileNames.Should().ContainSingle(name => name.StartsWith("guide-", StringComparison.Ordinal));

        var stored = await context.PaintingGuides.Include(g => g.Steps).SingleAsync();
        stored.Steps.OrderBy(step => step.SortOrder).First().ImageStorageKey.Should().StartWith("guide-");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsGuideSummaryWithStepCountAndCoverImage()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "artist");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var service = new PaintingGuideService(context, new StubImageService());
        var dto = CreateGuideDto();
        await service.CreateAsync(
            user.Id,
            dto,
            new Dictionary<int, IFormFile> { [1] = CreateFormFile(CreateJpegBytes(), "highlight.jpg", "image/jpeg") },
            CancellationToken.None);

        var result = await service.GetAllAsync(1, 10);

        result.Items.Should().ContainSingle();
        var summary = result.Items.Single();
        summary.Title.Should().Be(dto.Title);
        summary.StepCount.Should().Be(2);
        summary.CoverImageUrl.Should().StartWith("https://images.test/guide-");
    }

    [Fact]
    public async Task CreateAsync_WhenStepPhotoFileNameHasPath_ThrowsDomainValidationException()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "artist");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var service = new PaintingGuideService(context, new StubImageService());
        var dto = CreateGuideDto();
        var unsafePhoto = CreateFormFile(new byte[] { 1 }, "../basecoat.jpg", "image/jpeg");

        var act = async () => await service.CreateAsync(
            user.Id,
            dto,
            new Dictionary<int, IFormFile> { [0] = unsafePhoto },
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().ContainKey("StepPhotos");
    }

    [Fact]
    public async Task CreateAsync_WhenStepPhotoSourceDimensionsAreTooLarge_ThrowsUnsupportedImageDimensionsException()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "artist");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var service = new PaintingGuideService(
            context,
            new StubImageService(),
            imageOptions: Microsoft.Extensions.Options.Options.Create(new ImagesOptions
            {
                MaxSourceWidth = 8,
                MaxSourceHeight = 8,
                MaxSourceMegapixels = 1
            }));
        var dto = CreateGuideDto();
        var photo = CreateFormFile(CreateJpegBytes(), "large-source.jpg", "image/jpeg");

        var act = async () => await service.CreateAsync(
            user.Id,
            dto,
            new Dictionary<int, IFormFile> { [0] = photo },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnsupportedImageDimensionsException>();
    }

    [Fact]
    public async Task CreateAsync_WhenUploadConcurrencyLimitIsReached_ThrowsAndRollsBackGuide()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "artist");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var service = new PaintingGuideService(
            context,
            new StubImageService(),
            uploadConcurrencyLimiter: new RejectingUploadConcurrencyLimiter());
        var dto = CreateGuideDto();
        var photo = CreateFormFile(CreateJpegBytes(), "basecoat.jpg", "image/jpeg");

        var act = async () => await service.CreateAsync(
            user.Id,
            dto,
            new Dictionary<int, IFormFile> { [0] = photo },
            CancellationToken.None);

        await act.Should().ThrowAsync<UploadConcurrencyLimitExceededException>();
        (await context.PaintingGuides.CountAsync()).Should().Be(0);
    }

    private static CreatePaintingGuideDto CreateGuideDto() =>
        new()
        {
            Title = "Red cloak guide",
            Summary = "A step by step red cloak recipe.",
            Materials = "Red, brown, orange, ivory",
            Steps = new List<CreatePaintingGuideStepDto>
            {
                new()
                {
                    Title = "Basecoat",
                    Description = "Block in the main red.",
                    PaintsUsed = "Khorne Red",
                    Techniques = "Thin layers"
                },
                new()
                {
                    Title = "Highlight",
                    Description = "Push the folds with warm highlights.",
                    PaintsUsed = "Evil Sunz Scarlet, Ice Yellow",
                    Techniques = "Glazing"
                }
            }
        };

    private static IFormFile CreateFormFile(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "stepPhotos", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static byte[] CreateJpegBytes()
    {
        using var image = new Image<Rgba32>(16, 16);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }

    private sealed class StubImageService : IImageService
    {
        public List<string> UploadedFileNames { get; } = new();

        public Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            UploadedFileNames.Add(fileName);
            return Task.FromResult($"https://images.test/{fileName}");
        }

        public Task<Stream> DownloadAsync(string fileName) => Task.FromResult<Stream>(Stream.Null);

        public Task DeleteAsync(string fileName) => Task.CompletedTask;
    }

    private sealed class RejectingUploadConcurrencyLimiter : IUploadConcurrencyLimiter
    {
        public ValueTask<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IAsyncDisposable?>(null);
    }
}
