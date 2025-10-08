using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class PostsUploadTests : IClassFixture<PostsUploadTests.TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public PostsUploadTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadEndpoint_PersistsThreeVariantsAndReturnsMaxUrl()
    {
        await _factory.ResetAsync();
        await SeedUserAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        using var imageStream = await CreateImageAsync(1600, 900);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Title"), "Title");
        form.Add(new StringContent("Body"), "Content");

        var imageContent = new StreamContent(imageStream);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(imageContent, "images", "photo.jpg");

        var response = await client.PostAsync("/api/Posts/with-image", form);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto = await response.Content.ReadFromJsonAsync<PostDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        dto.Should().NotBeNull();
        dto!.ImageUrl.Should().NotBeNull();
        dto.ImageUrl.Should().Contain("_max.");

        var store = _factory.Services.GetRequiredService<TestImageStore>();
        store.Saved.Count.Should().Be(3);
        var names = store.Saved.Select(s => s.Name).ToList();
        names.Should().Contain(name => name.Contains("_max.", StringComparison.OrdinalIgnoreCase));
        names.Should().Contain(name => name.Contains("_preview.", StringComparison.OrdinalIgnoreCase));
        names.Should().Contain(name => name.Contains("_thumb.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UploadEndpoint_RejectsUnsupportedMimeType()
    {
        await _factory.ResetAsync();
        await SeedUserAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var bytes = new byte[1024];
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Title"), "Title");
        form.Add(new StringContent("Body"), "Content");

        var imageContent = new ByteArrayContent(bytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/heic");
        form.Add(imageContent, "images", "test.heic");

        var response = await client.PostAsync("/api/Posts/with-image", form);
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);

        var store = _factory.Services.GetRequiredService<TestImageStore>();
        store.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadEndpoint_RejectsOversizedUploads()
    {
        await _factory.ResetAsync();
        await SeedUserAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var bytes = new byte[20 * 1024 * 1024 + 1];
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Title"), "Title");
        form.Add(new StringContent("Body"), "Content");

        var imageContent = new ByteArrayContent(bytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(imageContent, "images", "large.jpg");

        var response = await client.PostAsync("/api/Posts/with-image", form);
        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        var store = _factory.Services.GetRequiredService<TestImageStore>();
        store.Saved.Should().BeEmpty();
    }

    private async Task SeedUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Users.RemoveRange(context.Users);
        context.Users.Add(new ApplicationUser
        {
            Id = TestAuthHandler.TestUserId,
            UserName = "tester",
            Email = "tester@example.com"
        });
        await context.SaveChangesAsync();
    }

    private static async Task<Stream> CreateImageAsync(int width, int height)
    {
        var image = new Image<Rgba32>(width, height);
        var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms);
        ms.Position = 0;
        return ms;
    }

    public class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

                services.RemoveAll(typeof(IImageStore));
                services.AddSingleton<TestImageStore>();
                services.AddSingleton<IImageStore>(sp => sp.GetRequiredService<TestImageStore>());

                services.RemoveAll(typeof(IImageService));
                services.AddSingleton<IImageService, FakeImageService>();

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                services.AddAuthorization(options =>
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                        .RequireAuthenticatedUser()
                        .Build();
                });
            });
        }

        public async Task ResetAsync()
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            scope.ServiceProvider.GetRequiredService<TestImageStore>().Clear();
        }
    }

    public class TestImageStore : IImageStore
    {
        private readonly List<StoredVariant> _saved = new();

        public IReadOnlyList<StoredVariant> Saved => _saved;

        public Task<ImageStoreResult> SaveAsync(Guid postId, Guid imageId, ImageVariants variants, System.Threading.CancellationToken ct)
        {
            lock (_saved)
            {
                SaveVariant(postId, imageId, "max", variants.Max);
                SaveVariant(postId, imageId, "preview", variants.Preview);
                SaveVariant(postId, imageId, "thumb", variants.Thumb);
            }

            var baseUrl = $"https://test.local/images/{postId:D}/";
            var result = new ImageStoreResult(
                baseUrl + $"{imageId:D}_max.{variants.Max.Extension}",
                baseUrl + $"{imageId:D}_preview.{variants.Preview.Extension}",
                baseUrl + $"{imageId:D}_thumb.{variants.Thumb.Extension}");
            return Task.FromResult(result);
        }

        public void Clear()
        {
            lock (_saved)
            {
                _saved.Clear();
            }
        }

        private void SaveVariant(Guid postId, Guid imageId, string suffix, ImageVariant variant)
        {
            _saved.Add(new StoredVariant($"{postId:D}_{imageId:D}_{suffix}.{variant.Extension}", variant));
        }

        public record StoredVariant(string Name, ImageVariant Variant);
    }

    public class FakeImageService : IImageService
    {
        public Task DeleteAsync(string fileName) => Task.CompletedTask;

        public Task<Stream> DownloadAsync(string fileName) => Task.FromResult<Stream>(new MemoryStream());

        public Task<string> UploadAsync(Stream fileStream, string fileName) => Task.FromResult($"https://test.local/{fileName}");
    }

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string TestUserId = "test-user";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId),
                new Claim(ClaimTypes.Name, "test@example.com")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
