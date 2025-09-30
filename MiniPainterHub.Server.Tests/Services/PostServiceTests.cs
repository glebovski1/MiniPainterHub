using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services;
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
        var service = new PostService(context);
        var dto = TestData.CreatePostDto();

        var act = async () => await service.CreateAsync(userId!, dto);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User must be authenticated to create posts.");
    }

    [Fact]
    public async Task CreateAsync_WhenUserDoesNotExist_ThrowsUnauthorizedAccessException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new PostService(context);
        var dto = TestData.CreatePostDto();

        var act = async () => await service.CreateAsync("missing", dto);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User must be authenticated to create posts.");
    }

    [Fact]
    public async Task CreateAsync_WhenValidUser_PersistsPostAndReturnsDto()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1", "artist");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var service = new PostService(context);
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
        var service = new PostService(context);

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
        var service = new PostService(context);

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
        var service = new PostService(context);
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
}

