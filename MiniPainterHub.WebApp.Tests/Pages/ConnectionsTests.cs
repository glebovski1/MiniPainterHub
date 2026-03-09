using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class ConnectionsTests : TestContext
{
    [Fact]
    public void RendersFollowersAndFollowingLists()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddFollowStub(new StubFollowService
        {
            GetFollowersHandler = () => Task.FromResult<IReadOnlyList<UserListItemDto>>(new[]
            {
                new UserListItemDto
                {
                    UserId = "follower-user",
                    UserName = "follower",
                    DisplayName = "Follower Painter"
                }
            }),
            GetFollowingHandler = () => Task.FromResult<IReadOnlyList<UserListItemDto>>(new[]
            {
                new UserListItemDto
                {
                    UserId = "following-user",
                    UserName = "following",
                    DisplayName = "Following Painter"
                }
            })
        });

        var cut = RenderComponent<Connections>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Followers");
            cut.Markup.Should().Contain("Following");
            cut.Markup.Should().Contain("Follower Painter");
            cut.Markup.Should().Contain("Following Painter");
        });
    }

    [Fact]
    public void WhenConnectionsAreEmpty_RendersEmptyStates()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddFollowStub();

        var cut = RenderComponent<Connections>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("No followers yet.");
            cut.Markup.Should().Contain("You are not following anyone yet.");
        });
    }
}
