using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services.Models;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class PostServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CreateAsync_WhenUserIdIsMissing_ThrowsUnauthorizedAccessException(string? userId)
    {
        await using var context = AppDbContextFactory.Create();
        var service = CreateService(context);
        var dto = TestData.CreatePostDto();

        var act = async () => await service.CreateAsync(userId!, dto);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User must be authenticated to create posts.");
    }

    [Fact]
    public async Task CreateAsync_WhenUserDoesNotExist_ThrowsUnauthorizedAccessException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = CreateService(context);
        var dto = TestData.CreatePostDto();

        var act = async () => await service.CreateAsync("missing", dto);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User must be authenticated to create posts.");
    }

    [Theory]
    [InlineData(0, 10, "page", "Page number must be at least 1.")]
    [InlineData(-1, 5, "page", "Page number must be at least 1.")]
    [InlineData(1, 0, "pageSize", "Page size must be greater than 0.")]
    [InlineData(2, -3, "pageSize", "Page size must be greater than 0.")]
    public async Task GetAllAsync_WhenPaginationIsInvalid_ThrowsDomainValidationException(
        int page,
        int pageSize,
        string expectedKey,
        string expectedMessage)
    {
        await using var context = AppDbContextFactory.Create();
        var service = CreateService(context);

        var act = async () => await service.GetAllAsync(page, pageSize);

        var exception = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Pagination parameters are invalid.");

        exception.Which.Errors.Should().ContainKey(expectedKey)
            .WhoseValue.Should().Contain(expectedMessage);
    }

    [Fact]
    public async Task GetAllAsync_WhenPageAndPageSizeInvalid_ThrowsDomainValidationExceptionWithAllErrors()
    {
        await using var context = AppDbContextFactory.Create();
        var service = CreateService(context);

        var act = async () => await service.GetAllAsync(0, 0);

        var exception = await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Pagination parameters are invalid.");

        exception.Which.Errors.Should()
            .ContainKey("page").WhoseValue.Should().Contain("Page number must be at least 1.");
        exception.Which.Errors.Should()
            .ContainKey("pageSize").WhoseValue.Should().Contain("Page size must be greater than 0.");
    }

    [Fact]
    public async Task CreateAsync_WhenValidUser_PersistsPostAndReturnsDto()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "artist");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var dto = TestData.CreatePostDto(imageCount: 6);

        var result = await service.CreateAsync(user.Id, dto);

        result.Should().BeEquivalentTo(new
        {
            CreatedById = user.Id,
            Title = dto.Title,
            Content = dto.Content,
            AuthorName = user.UserName,
            Images = dto.Images!.Take(5).Select(i => new { i.ImageUrl, i.ThumbnailUrl }).ToList()
        }, options => options.ExcludingMissingMembers());
        result.Images.Should().HaveCount(5);
        result.ImageUrl.Should().Be(dto.Images.First().ImageUrl);

        var storedPost = await context.Posts.Include(p => p.Images).SingleAsync();
        storedPost.CreatedById.Should().Be(user.Id);
        storedPost.Images.Should().HaveCount(5);
        storedPost.Images.Select(i => i.ImageUrl)
            .Should().BeEquivalentTo(dto.Images.Take(5).Select(i => i.ImageUrl));
    }

    [Fact]
    public async Task DeleteAsync_WhenPostDoesNotExist_ThrowsNotFoundException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = CreateService(context);

        var act = async () => await service.DeleteAsync(42, "user-1");

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Post not found.");
    }

    [Fact]
    public async Task DeleteAsync_WhenPostBelongsToUser_SoftDeletesPost()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(1, user.Id);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.DeleteAsync(post.Id, user.Id);

        result.Should().BeTrue();
        var storedPost = await context.Posts.SingleAsync();
        storedPost.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task AddImagesAsync_WhenPostHasMaxImages_DoesNotExceedLimit()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(1, user.Id, imageCount: 4);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var newImages = TestData.CreateImages(3, "extra").ToList();
        var existingImageUrls = post.Images.Select(i => i.ImageUrl).ToList();

        var result = await service.AddImagesAsync(post.Id, newImages);

        // Capped at 5 total
        result.Should().HaveCount(5);

        // No duplicate IDs or URLs
        result.Should().OnlyHaveUniqueItems(i => i.Id);
        result.Select(i => i.ImageUrl).Should().OnlyHaveUniqueItems();

        // Content check: all existing + first new image only
        result.Select(i => i.ImageUrl)
            .Should().BeEquivalentTo(existingImageUrls.Concat(new[] { newImages.First().ImageUrl }));

        // DB state mirrors expectations
        var storedPost = await context.Posts.Include(p => p.Images).SingleAsync();
        storedPost.Images.Should().HaveCount(5);
        storedPost.Images.Select(i => i.Id).Should().OnlyHaveUniqueItems();
    }

    private static PostService CreateService(AppDbContext context)
    {
        return new PostService(
            context,
            new StubImageService(),
            new StubImageProcessor(),
            new StubImageStore(),
            Options.Create(new ImagesOptions()),
            NullLogger<PostService>.Instance);
    }

    private sealed class StubImageService : IImageService
    {
        public Task DeleteAsync(string fileName) => Task.CompletedTask;

        public Task<Stream> DownloadAsync(string fileName) => Task.FromResult<Stream>(Stream.Null);

        public Task<string> UploadAsync(Stream fileStream, string fileName) => Task.FromResult($"https://test.local/{fileName}");
    }

    private sealed class StubImageProcessor : IImageProcessor
    {
        public Task<ImageVariants> ProcessAsync(Stream stream, string contentType, CancellationToken ct)
        {
            var variant = new ImageVariant(new byte[] { 1 }, "image/jpeg", "jpg", 1, 1);
            return Task.FromResult(new ImageVariants(variant, variant, variant));
        }
    }

    private sealed class StubImageStore : IImageStore
    {
        public Task<ImageStoreResult> SaveAsync(Guid postId, Guid imageId, ImageVariants variants, CancellationToken ct)
        {
            var baseUrl = $"https://test.local/{postId:D}/";
            return Task.FromResult(new ImageStoreResult(
                baseUrl + $"{imageId:D}_max.jpg",
                baseUrl + $"{imageId:D}_preview.jpg",
                baseUrl + $"{imageId:D}_thumb.jpg"));
        }
    }

}

