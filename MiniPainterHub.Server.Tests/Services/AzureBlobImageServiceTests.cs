using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using FluentAssertions;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services;
using Moq;
using Xunit;
using Microsoft.Extensions.Options;

namespace MiniPainterHub.Server.Tests.Services;

public class AzureBlobImageServiceTests
{
    [Fact]
    public async Task DownloadAsync_WhenBlobIsMissing_ThrowsNotFoundException()
    {
        var containerMock = new Mock<BlobContainerClient>();
        var blobMock = new Mock<BlobClient>();
        containerMock.Setup(c => c.GetBlobClient("missing.png"))
            .Returns(blobMock.Object);

        blobMock.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

        var service = new AzureBlobImageService(containerMock.Object, Options.Create(new ImagesOptions()));

        var act = async () => await service.DownloadAsync("missing.png");

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Image 'missing.png' not found.");
    }
}
