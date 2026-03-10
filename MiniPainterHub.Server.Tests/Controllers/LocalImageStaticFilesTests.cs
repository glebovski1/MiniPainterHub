using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniPainterHub.Server.Data;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class LocalImageStaticFilesTests : IClassFixture<LocalImageStaticFilesTests.TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public LocalImageStaticFilesTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DevelopmentStaticFiles_ServesImagesFromConfiguredPhysicalFolder()
    {
        var client = _factory.CreateClient();
        var fileName = "persisted-image.webp";
        var expected = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(Path.Combine(_factory.ImageRoot, fileName), expected);

        var response = await client.GetAsync($"/uploads/images/{fileName}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(expected);
    }

    public sealed class TestApplicationFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly string _databaseName = Guid.NewGuid().ToString("N");

        public TestApplicationFactory()
        {
            ImageRoot = Path.Combine(Path.GetTempPath(), "MiniPainterHub.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ImageRoot);
        }

        public string ImageRoot { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ImageStorage:LocalPath"] = ImageRoot,
                    ["ImageStorage:RequestPath"] = "/uploads/images"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && Directory.Exists(ImageRoot))
            {
                Directory.Delete(ImageRoot, recursive: true);
            }
        }
    }
}
