using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services;
using Moq;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class LocalImageServiceTests
{
    [Fact]
    public async Task DownloadAsync_WhenFileIsMissing_ThrowsNotFoundException()
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
                    ["ImageStorage:LocalPath"] = "images"
                })
                .Build();

            var service = new LocalImageService(envMock.Object, config);

            var act = async () => await service.DownloadAsync("missing.png");

            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("Image 'missing.png' not found.");
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
