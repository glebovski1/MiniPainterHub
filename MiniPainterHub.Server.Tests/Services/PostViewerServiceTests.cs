using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class PostViewerServiceTests
{
    [Fact]
    public async Task GetAsync_WhenLegacyLocalImageHasNoUsableVariants_ReturnsGeneratedVariantEndpoints()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("author-1");
        var post = TestData.CreatePost(19, user.Id);
        post.Images.Add(new PostImage
        {
            Id = 101,
            PostId = post.Id,
            ImageUrl = "/uploads/images/seed-post-19.png",
            PreviewUrl = "/uploads/images/seed-post-19.png",
            ThumbnailUrl = "/uploads/images/seed-post-19.png",
            Width = 1024,
            Height = 1536
        });
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAsync(post.Id, currentUserId: null);

        result.Images.Should().ContainSingle();
        result.Images[0].ImageUrl.Should().Be("/uploads/images/seed-post-19.png");
        result.Images[0].PreviewUrl.Should().Be("/api/images/preview?url=%2Fuploads%2Fimages%2Fseed-post-19.png");
        result.Images[0].ThumbnailUrl.Should().Be("/api/images/thumbnail?url=%2Fuploads%2Fimages%2Fseed-post-19.png");
    }

    [Fact]
    public async Task GetAsync_WhenStoredVariantsAreDistinct_ReturnsStoredVariantUrls()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("author-1");
        var post = TestData.CreatePost(20, user.Id);
        post.Images.Add(new PostImage
        {
            Id = 102,
            PostId = post.Id,
            ImageUrl = "/uploads/images/full.webp",
            PreviewUrl = "/uploads/images/preview.webp",
            ThumbnailUrl = "/uploads/images/thumb.webp"
        });
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetAsync(post.Id, currentUserId: user.Id);

        result.Images.Should().ContainSingle();
        result.Images[0].PreviewUrl.Should().Be("/uploads/images/preview.webp");
        result.Images[0].ThumbnailUrl.Should().Be("/uploads/images/thumb.webp");
    }

    [Fact]
    public async Task GetExperienceAsync_ReturnsPostViewerAndViewerSpecificLikeState()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("author-1");
        var post = TestData.CreatePost(21, user.Id);
        post.Images.Add(new PostImage
        {
            Id = 103,
            PostId = post.Id,
            ImageUrl = "/uploads/images/experience.webp"
        });
        context.Users.Add(user);
        context.Posts.Add(post);
        context.Likes.Add(new Like
        {
            PostId = post.Id,
            UserId = user.Id,
            CreatedAt = System.DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.GetExperienceAsync(post.Id, user.Id);

        result.Post.Id.Should().Be(post.Id);
        result.Post.Images.Should().ContainSingle();
        result.Viewer.PostId.Should().Be(post.Id);
        result.Viewer.CanManageAuthorMarks.Should().BeTrue();
        result.Likes.Should().BeEquivalentTo(new MiniPainterHub.Common.DTOs.LikeDto
        {
            PostId = post.Id,
            Count = 1,
            UserHasLiked = true
        });
    }

    private static PostViewerService CreateService(AppDbContext context) => new(context);
}
