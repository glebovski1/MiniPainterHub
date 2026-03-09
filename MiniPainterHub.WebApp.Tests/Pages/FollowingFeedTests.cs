using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class FollowingFeedTests : TestContext
{
    [Fact]
    public void RendersPostsFromFollowingFeed()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddPostStub(new StubPostService
        {
            GetFollowingFeedHandler = (_, _) => Task.FromResult(
                new ApiResult<PagedResult<PostSummaryDto>>(
                    true,
                    HttpStatusCode.OK,
                    new PagedResult<PostSummaryDto>
                    {
                        Items = new[]
                        {
                            new PostSummaryDto
                            {
                                Id = 1,
                                Title = "Followed post",
                                Snippet = "Snippet",
                                AuthorId = "target-user",
                                AuthorName = "Target Painter"
                            }
                        },
                        PageNumber = 1,
                        PageSize = 9,
                        TotalCount = 1
                    }))
        });

        var cut = RenderComponent<FollowingFeed>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Following feed");
            cut.Markup.Should().Contain("Followed post");
        });
    }

    [Fact]
    public void WhenFollowingFeedIsEmpty_RendersEmptyState()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddPostStub(new StubPostService
        {
            GetFollowingFeedHandler = (_, _) => Task.FromResult(
                new ApiResult<PagedResult<PostSummaryDto>>(
                    true,
                    HttpStatusCode.OK,
                    new PagedResult<PostSummaryDto>
                    {
                        Items = new PostSummaryDto[0],
                        PageNumber = 1,
                        PageSize = 9,
                        TotalCount = 0
                    }))
        });

        var cut = RenderComponent<FollowingFeed>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Following feed");
            cut.Markup.Should().Contain("No posts yet.");
        });
    }
}
