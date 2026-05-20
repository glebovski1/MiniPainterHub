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
            Images = dto.Images!.Take(6).Select(i => new { i.ImageUrl, i.ThumbnailUrl }).ToList()
        }, options => options.ExcludingMissingMembers());
        result.Images.Should().HaveCount(6);
        result.ImageUrl.Should().Be(dto.Images!.First().ImageUrl);

        var storedPost = await context.Posts.Include(p => p.Images).SingleAsync();
        storedPost.CreatedById.Should().Be(user.Id);
        storedPost.Images.Should().HaveCount(6);
        storedPost.Images.Select(i => i.ImageUrl)
            .Should().BeEquivalentTo(dto.Images!.Take(6).Select(i => i.ImageUrl));
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

        // Capped at 8 total
        result.Should().HaveCount(7);

        // No duplicate IDs or URLs
        result.Should().OnlyHaveUniqueItems(i => i.Id);
        result.Select(i => i.ImageUrl).Should().OnlyHaveUniqueItems();

        // Content check: all existing + all new images while still below the cap
        result.Select(i => i.ImageUrl)
            .Should().BeEquivalentTo(existingImageUrls.Concat(newImages.Select(image => image.ImageUrl)));

        // DB state mirrors expectations
        var storedPost = await context.Posts.Include(p => p.Images).SingleAsync();
        storedPost.Images.Should().HaveCount(7);
        storedPost.Images.Select(i => i.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ExistsAsync_WhenActivePostExists_ReturnsTrue()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(1, user.Id);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var exists = await service.ExistsAsync(post.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenPostIsMissingOrDeleted_ReturnsFalse()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var deletedPost = TestData.CreatePost(1, user.Id, isDeleted: true);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(deletedPost);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var missingExists = await service.ExistsAsync(999);
        var deletedExists = await service.ExistsAsync(deletedPost.Id);

        missingExists.Should().BeFalse();
        deletedExists.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_WhenIncludeDeletedTrue_ReturnsVisibleAndHiddenPosts()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var visiblePost = TestData.CreatePost(10, user.Id, isDeleted: false);
        var hiddenPost = TestData.CreatePost(11, user.Id, isDeleted: true);
        await context.Users.AddAsync(user);
        await context.Posts.AddRangeAsync(visiblePost, hiddenPost);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAllAsync(1, 10, includeDeleted: true, deletedOnly: false);

        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(item => item.Id == visiblePost.Id && !item.IsDeleted);
        result.Items.Should().Contain(item => item.Id == hiddenPost.Id && item.IsDeleted);
    }

    [Fact]
    public async Task GetAllAsync_WhenDeletedOnlyTrue_ReturnsOnlyHiddenPosts()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var visiblePost = TestData.CreatePost(20, user.Id, isDeleted: false);
        var hiddenPost = TestData.CreatePost(21, user.Id, isDeleted: true);
        await context.Users.AddAsync(user);
        await context.Posts.AddRangeAsync(visiblePost, hiddenPost);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAllAsync(1, 10, includeDeleted: true, deletedOnly: true);

        result.Items.Should().ContainSingle();
        result.Items.Single().Id.Should().Be(hiddenPost.Id);
        result.Items.Single().IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_WhenPostHasThumbnail_ReturnsSummaryThumbnailUrl()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(24, user.Id, imageCount: 1);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAllAsync(1, 10);

        result.Items.Should().ContainSingle();
        result.Items.Single().ImageUrl.Should().Be("https://img/24/0");
        result.Items.Single().ThumbnailUrl.Should().Be("https://thumb/24/0");
    }

    [Fact]
    public async Task GetAllAsync_WhenLegacyLocalImageHasNoThumbnail_ReturnsThumbnailEndpoint()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(25, user.Id);
        post.Images.Add(new MiniPainterHub.Server.Entities.PostImage
        {
            PostId = post.Id,
            ImageUrl = "/uploads/images/seed-post-01.png",
            PreviewUrl = "/uploads/images/seed-post-01.png",
            ThumbnailUrl = "/uploads/images/seed-post-01.png"
        });
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAllAsync(1, 10);

        result.Items.Should().ContainSingle();
        result.Items.Single().ThumbnailUrl.Should().Be("/api/images/thumbnail?url=%2Fuploads%2Fimages%2Fseed-post-01.png");
    }

    [Fact]
    public async Task GetTopPostsAsync_ReturnsMostLikedRecentPostsWithinLimit()
    {
        await using var context = AppDbContextFactory.Create();
        var author = TestData.CreateUser("author-1");
        var liker1 = TestData.CreateUser("liker-1");
        var liker2 = TestData.CreateUser("liker-2");
        var recentLow = TestData.CreatePost(30, author.Id);
        recentLow.CreatedUtc = DateTime.UtcNow.AddDays(-1);
        var recentHigh = TestData.CreatePost(31, author.Id);
        recentHigh.CreatedUtc = DateTime.UtcNow.AddDays(-2);
        var oldHigh = TestData.CreatePost(32, author.Id);
        oldHigh.CreatedUtc = DateTime.UtcNow.AddDays(-90);
        await context.Users.AddRangeAsync(author, liker1, liker2);
        await context.Posts.AddRangeAsync(recentLow, recentHigh, oldHigh);
        await context.Likes.AddRangeAsync(
            new MiniPainterHub.Server.Entities.Like { PostId = recentHigh.Id, UserId = liker1.Id },
            new MiniPainterHub.Server.Entities.Like { PostId = recentHigh.Id, UserId = liker2.Id },
            new MiniPainterHub.Server.Entities.Like { PostId = oldHigh.Id, UserId = liker1.Id },
            new MiniPainterHub.Server.Entities.Like { PostId = oldHigh.Id, UserId = liker2.Id });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetTopPostsAsync(count: 1, TimeSpan.FromDays(30));

        result.Should().ContainSingle();
        result.Single().Id.Should().Be(recentHigh.Id);
        result.Single().LikeCount.Should().Be(2);
    }

    [Theory]
    [InlineData(0, 30, "count")]
    [InlineData(21, 30, "count")]
    [InlineData(5, 0, "lookbackDays")]
    [InlineData(5, 366, "lookbackDays")]
    public async Task GetTopPostsAsync_WhenArgumentsAreInvalid_ThrowsDomainValidationException(
        int count,
        int lookbackDays,
        string expectedKey)
    {
        await using var context = AppDbContextFactory.Create();
        var service = CreateService(context);

        var act = async () => await service.GetTopPostsAsync(count, TimeSpan.FromDays(lookbackDays));

        var exception = await act.Should().ThrowAsync<DomainValidationException>();
        exception.Which.Errors.Should().ContainKey(expectedKey);
    }

    private static PostService CreateService(AppDbContext context)
    {
        return new PostService(
            context,
            new StubImageService(),
            new StubImageProcessor(),
            new StubImageStore(),
            Microsoft.Extensions.Options.Options.Create(new ImagesOptions()),
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
        public Task<ImageVariants> ProcessAsync(Stream stream, string? contentType, CancellationToken ct)
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

        public Task DeleteAsync(Guid postId, Guid imageId, CancellationToken ct) => Task.CompletedTask;
    }

}

