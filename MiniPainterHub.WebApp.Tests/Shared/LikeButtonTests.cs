using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class LikeButtonTests : TestContext
{
    [Fact]
    public void InitialRender_LoadsLikeStateFromService()
    {
        this.AddLikeStub(new StubLikeService
        {
            GetLikesHandler = _ => Task.FromResult(new LikeDto
            {
                Count = 4,
                UserHasLiked = false
            })
        });

        var cut = RenderComponent<LikeButton>(parameters => parameters
            .Add(p => p.PostId, 101)
            .Add(p => p.TestId, "post-like-toggle"));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='post-like-toggle-count']").TextContent.Should().Be("4"));
    }

    [Fact]
    public void Click_TogglesLikeAndUpdatesCount()
    {
        var liked = false;
        this.AddLikeStub(new StubLikeService
        {
            GetLikesHandler = _ => Task.FromResult(new LikeDto
            {
                Count = 2,
                UserHasLiked = false
            }),
            ToggleLikeHandler = _ =>
            {
                liked = !liked;
                return Task.FromResult(new LikeDto
                {
                    Count = liked ? 3 : 2,
                    UserHasLiked = liked
                });
            }
        });

        var cut = RenderComponent<LikeButton>(parameters => parameters
            .Add(p => p.PostId, 101)
            .Add(p => p.TestId, "post-like-toggle"));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='post-like-toggle-count']").TextContent.Should().Be("2"));

        cut.Find("[data-testid='post-like-toggle']").Click();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='post-like-toggle-count']").TextContent.Should().Be("3"));
    }
}
