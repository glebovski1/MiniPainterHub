using System.Security.Claims;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Posts;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Posts;

public class MyPostsTests : TestContext
{
    [Fact]
    public void WhenAuthenticatedAndProfileIsMissing_DoesNotRedirectToLogin()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("claims-user");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        this.AddProfileStub(new StubProfileService
        {
            GetMineHandler = () => Task.FromResult<UserProfileDto?>(null)
        });
        this.AddPostStub(new StubPostService
        {
            GetAllHandler = (_, _) => Task.FromResult(
                new ApiResult<PagedResult<PostSummaryDto>>(
                    true,
                    HttpStatusCode.OK,
                    new PagedResult<PostSummaryDto>
                    {
                        Items = new List<PostSummaryDto>(),
                        PageNumber = 1,
                        PageSize = 9,
                        TotalCount = 0
                    }))
        });

        var cut = RenderComponent<MyPosts>();

        cut.WaitForAssertion(() =>
        {
            this.CurrentPath().Should().NotBe("/login");
            cut.Markup.Should().Contain("claims-user");
        });
    }

    [Fact]
    public void WhenUnauthenticated_RedirectsToLogin()
    {
        var auth = this.AddTestAuthorization();
        auth.SetNotAuthorized();
        this.AddProfileStub();
        this.AddPostStub();

        var cut = RenderComponent<MyPosts>();

        cut.WaitForAssertion(() =>
        {
            this.CurrentPath().Should().Be("/login");
        });
    }
}
