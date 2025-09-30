using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class LikeServiceTests
{
    [Fact]
    public async Task GetLikesAsync_WhenPostDoesNotExist_ThrowsNotFoundException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new LikeService(context);

        var act = async () => await service.GetLikesAsync(1, "user-1");

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Post 1 not found.");
    }

    [Fact]
    public async Task GetLikesAsync_WhenPostExists_ReturnsTotalCountAndFlag()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(1, user.Id);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.Likes.AddRangeAsync(
            new Like { PostId = post.Id, UserId = "user-1" },
            new Like { PostId = post.Id, UserId = "user-2" });
        await context.SaveChangesAsync();
        var service = new LikeService(context);

        var result = await service.GetLikesAsync(post.Id, "user-1");

        result.Count.Should().Be(2);
        result.UserHasLiked.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleAsync_WhenLikeExists_RemovesIt()
    {
        await using var context = AppDbContextFactory.Create();
        var user = TestData.CreateUser("user-1");
        var post = TestData.CreatePost(1, user.Id);
        await context.Users.AddAsync(user);
        await context.Posts.AddAsync(post);
        await context.Likes.AddAsync(new Like { PostId = post.Id, UserId = user.Id });
        await context.SaveChangesAsync();
        var service = new LikeService(context);

        var result = await service.ToggleAsync(post.Id, user.Id);

        result.Should().BeTrue();
        (await context.Likes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ToggleAsync_WhenPostIsMissing_ThrowsNotFoundException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new LikeService(context);

        var act = async () => await service.ToggleAsync(1, "user-1");

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Post 1 not found.");
    }

    [Fact]
    public async Task RemoveAsync_WhenLikeDoesNotExist_ThrowsNotFoundException()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new LikeService(context);

        var act = async () => await service.RemoveAsync(1, "user-1");

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Like not found.");
    }
}
