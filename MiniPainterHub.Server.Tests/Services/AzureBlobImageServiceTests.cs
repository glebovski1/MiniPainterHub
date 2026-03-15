using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;
using Moq;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class AzureBlobImageServiceTests
{
    [Fact]
    public void Ctor_WhenContainerIsNull_ThrowsArgumentNullException()
    {
        var act = () => new AzureBlobImageService((BlobContainerClient)null!,  Microsoft.Extensions.Options.Options.Create(new ImagesOptions()));

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("container");
    }

    [Fact]
    public void Ctor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var act = () => new AzureBlobImageService(Mock.Of<BlobContainerClient>(), null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void ServiceProvider_CanResolveAzureBlobImageService_FromBlobContainerRegistration()
    {
        var services = new ServiceCollection();
        services.AddOptions<ImagesOptions>()
            .Configure(_ => { });
        services.AddSingleton(Mock.Of<BlobContainerClient>());
        services.AddSingleton<AzureBlobImageService>();
        services.AddSingleton<IImageService>(sp => sp.GetRequiredService<AzureBlobImageService>());
        services.AddSingleton<IImageStore>(sp => sp.GetRequiredService<AzureBlobImageService>());

        using var provider = services.BuildServiceProvider();

        var imageService = provider.GetRequiredService<IImageService>();
        var imageStore = provider.GetRequiredService<IImageStore>();

        imageService.Should().BeOfType<AzureBlobImageService>();
        imageStore.Should().BeSameAs(imageService);
    }

    [Fact]
    public async Task UploadAsync_UploadsBlobAndReturnsBlobUri()
    {
        var containerMock = new Mock<BlobContainerClient>();
        var blobMock = CreateBlobMock("simple.png");

        containerMock.Setup(c => c.GetBlobClient("simple.png"))
            .Returns(blobMock.Object);

        var service = new AzureBlobImageService(containerMock.Object, Microsoft.Extensions.Options.Options.Create(new ImagesOptions()));
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var result = await service.UploadAsync(stream, "simple.png");

        result.Should().Be("https://storage.example/simple.png");
        blobMock.Verify(
            b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveAsync_WhenVariantsAreNull_ThrowsArgumentNullException()
    {
        var service = new AzureBlobImageService(Mock.Of<BlobContainerClient>(), Microsoft.Extensions.Options.Options.Create(new ImagesOptions()));

        var act = async () => await service.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("variants");
    }

    [Fact]
    public async Task SaveAsync_WhenKeepOriginalDisabled_StoresThreeVariants()
    {
        var containerMock = new Mock<BlobContainerClient>();
        var requestedNames = new List<string>();

        containerMock.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns((string name) =>
            {
                requestedNames.Add(name);
                return CreateBlobMock(name).Object;
            });

        var service = new AzureBlobImageService(containerMock.Object, Microsoft.Extensions.Options.Options.Create(new ImagesOptions
        {
            KeepOriginal = false
        }));

        var postId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var imageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var result = await service.SaveAsync(postId, imageId, CreateVariants(includeOriginal: true), CancellationToken.None);

        result.MaxUrl.Should().Contain("_max.");
        result.PreviewUrl.Should().Contain("_preview.");
        result.ThumbUrl.Should().Contain("_thumb.");
        result.OriginalUrl.Should().BeNull();
        requestedNames.Should().HaveCount(3);
        requestedNames.Should().NotContain(n => n.Contains("_original."));
    }

    [Fact]
    public async Task SaveAsync_WhenKeepOriginalEnabled_StoresOriginalVariant()
    {
        var containerMock = new Mock<BlobContainerClient>();
        var requestedNames = new List<string>();

        containerMock.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns((string name) =>
            {
                requestedNames.Add(name);
                return CreateBlobMock(name).Object;
            });

        var service = new AzureBlobImageService(containerMock.Object, Microsoft.Extensions.Options.Options.Create(new ImagesOptions
        {
            KeepOriginal = true
        }));

        var postId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var imageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var result = await service.SaveAsync(postId, imageId, CreateVariants(includeOriginal: true), CancellationToken.None);

        result.OriginalUrl.Should().Contain("_original.");
        requestedNames.Should().HaveCount(4);
        requestedNames.Should().Contain(n => n.Contains("_original."));
    }

    [Fact]
    public async Task DownloadAsync_WhenBlobIsMissing_ThrowsNotFoundException()
    {
        var containerMock = new Mock<BlobContainerClient>();
        var blobMock = new Mock<BlobClient>();
        containerMock.Setup(c => c.GetBlobClient("missing.png"))
            .Returns(blobMock.Object);

        blobMock.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

        var service = new AzureBlobImageService(containerMock.Object, Microsoft.Extensions.Options.Options.Create(new ImagesOptions()));

        var act = async () => await service.DownloadAsync("missing.png");

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Image 'missing.png' not found.");
    }

    [Fact]
    public async Task DeleteAsync_DeletesBlobIfExists()
    {
        var containerMock = new Mock<BlobContainerClient>();
        var blobMock = CreateBlobMock("to-delete.png");
        containerMock.Setup(c => c.GetBlobClient("to-delete.png")).Returns(blobMock.Object);

        var service = new AzureBlobImageService(containerMock.Object, Microsoft.Extensions.Options.Options.Create(new ImagesOptions()));
        await service.DeleteAsync("to-delete.png");

        blobMock.Verify(
            b => b.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenStoredVariantsExist_DeletesEveryMatchingBlobAndIsIdempotent()
    {
        var containerMock = new Mock<BlobContainerClient>();
        var postId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var imageId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var blobNames = new[]
        {
            $"images/{postId:D}/{imageId:D}_max.webp",
            $"images/{postId:D}/{imageId:D}_preview.webp",
            $"images/{postId:D}/{imageId:D}_thumb.webp",
            $"images/{postId:D}/{imageId:D}_original.jpg"
        };

        var blobs = new Dictionary<string, Mock<BlobClient>>();
        foreach (var blobName in blobNames)
        {
            blobs[blobName] = CreateBlobMock(blobName);
            containerMock.Setup(c => c.GetBlobClient(blobName)).Returns(blobs[blobName].Object);
        }

        var page = Page<BlobItem>.FromValues(
            new[]
            {
                BlobsModelFactory.BlobItem(name: blobNames[0]),
                BlobsModelFactory.BlobItem(name: blobNames[1]),
                BlobsModelFactory.BlobItem(name: blobNames[2]),
                BlobsModelFactory.BlobItem(name: blobNames[3])
            },
            continuationToken: null,
            Mock.Of<Response>());

        containerMock.Setup(c => c.GetBlobsAsync(
                It.IsAny<BlobTraits>(),
                It.IsAny<BlobStates>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<BlobItem>.FromPages(new[] { page }));

        var service = new AzureBlobImageService(containerMock.Object, Microsoft.Extensions.Options.Options.Create(new ImagesOptions()));

        await service.DeleteAsync(postId, imageId, CancellationToken.None);
        await service.DeleteAsync(postId, imageId, CancellationToken.None);

        foreach (var blobName in blobNames)
        {
            blobs[blobName].Verify(
                b => b.DeleteIfExistsAsync(
                    It.IsAny<DeleteSnapshotsOption>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }
    }

    private static Mock<BlobClient> CreateBlobMock(string fileName)
    {
        var blobMock = new Mock<BlobClient>();
        blobMock.SetupGet(b => b.Uri).Returns(new Uri($"https://storage.example/{fileName}"));
        blobMock.Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());
        blobMock.Setup(b => b.SetHttpHeadersAsync(It.IsAny<BlobHttpHeaders>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<BlobInfo>>());
        blobMock.Setup(b => b.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        return blobMock;
    }

    private static ImageVariants CreateVariants(bool includeOriginal)
    {
        var max = new ImageVariant(new byte[] { 1, 2, 3 }, "image/webp", "webp", 1200, 800);
        var preview = new ImageVariant(new byte[] { 4, 5, 6 }, "image/webp", "webp", 640, 480);
        var thumb = new ImageVariant(new byte[] { 7, 8, 9 }, "image/webp", "webp", 320, 240);
        var original = includeOriginal ? new ImageVariant(new byte[] { 10, 11, 12 }, "image/jpeg", "jpg", 2000, 1500) : null;
        return new ImageVariants(max, preview, thumb, original);
    }
}
