using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class LocalImageServiceTests
{
    [Theory]
    [InlineData("../escape.jpg")]
    [InlineData("nested/../escape.jpg")]
    [InlineData("C:/outside.jpg")]
    public async Task UploadAsync_WhenStorageKeyEscapesRoot_ThrowsDomainValidationException(string fileName)
    {
        var root = CreateTempDirectory();
        try
        {
            var service = CreateService(root);
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("image"));

            var act = async () => await service.UploadAsync(stream, fileName);

            await act.Should().ThrowAsync<DomainValidationException>()
                .WithMessage("Invalid image storage key.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WhenStorageKeyEscapesRoot_ThrowsDomainValidationException()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = CreateService(root);

            var act = async () => await service.DownloadAsync("../escape.jpg");

            await act.Should().ThrowAsync<DomainValidationException>()
                .WithMessage("Invalid image storage key.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UploadAsync_WhenStorageKeyIsNestedButSafe_WritesUnderRoot()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = CreateService(root);
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("image"));

            var url = await service.UploadAsync(stream, "safe/nested.jpg");

            url.Should().Be("/uploads/images/safe%2Fnested.jpg");
            File.Exists(Path.Combine(root, "safe", "nested.jpg")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static LocalImageService CreateService(string root)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.WebRootPath).Returns(root);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImageStorage:LocalPath"] = root,
                ["ImageStorage:RequestPath"] = "/uploads/images"
            })
            .Build();

        return new LocalImageService(env.Object, configuration, Microsoft.Extensions.Options.Options.Create(new ImagesOptions()));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "MiniPainterHubTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
