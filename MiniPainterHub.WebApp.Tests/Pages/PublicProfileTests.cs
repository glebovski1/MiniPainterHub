using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class PublicProfileTests : TestContext
{
    [Fact]
    public void WhenViewingAnotherUser_RendersFollowAndMessageActions()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddProfileStub(new StubProfileService
        {
            GetByIdHandler = id => Task.FromResult(new UserProfileDto
            {
                UserId = id,
                UserName = "target",
                DisplayName = "Target Painter",
                FollowerCount = 5,
                FollowingCount = 3,
                CanMessage = true,
                IsFollowing = false
            })
        });
        this.AddFollowStub();
        this.AddConversationStub();
        this.AddPostStub();

        var cut = RenderComponent<PublicProfile>(parameters => parameters.Add(p => p.UserId, "target-user"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Target Painter");
            cut.Find("[data-testid='profile-follow-toggle']").TextContent.Should().Contain("Follow");
            cut.Find("[data-testid='profile-message']").Should().NotBeNull();
        });
    }

    [Fact]
    public void MessageButton_StartsConversationAndNavigatesToMessages()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddProfileStub(new StubProfileService
        {
            GetByIdHandler = id => Task.FromResult(new UserProfileDto
            {
                UserId = id,
                UserName = "target",
                DisplayName = "Target Painter",
                CanMessage = true
            })
        });
        this.AddFollowStub();
        this.AddPostStub();
        this.AddConversationStub(new StubConversationService
        {
            OpenDirectConversationHandler = userId => Task.FromResult(new ConversationSummaryDto
            {
                Id = 7,
                OtherUser = new UserListItemDto { UserId = userId, UserName = "target", DisplayName = "Target Painter" }
            })
        });

        var cut = RenderComponent<PublicProfile>(parameters => parameters.Add(p => p.UserId, "target-user"));
        cut.WaitForElement("[data-testid='profile-message']");

        cut.Find("[data-testid='profile-message']").Click();

        this.CurrentPath().Should().Be("/messages/7");
    }

    [Fact]
    public void FollowToggle_FollowsAndRefreshesProfileState()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");

        var isFollowing = false;
        var followCalls = new List<string>();

        this.AddProfileStub(new StubProfileService
        {
            GetByIdHandler = id => Task.FromResult(new UserProfileDto
            {
                UserId = id,
                UserName = "target",
                DisplayName = "Target Painter",
                CanMessage = true,
                IsFollowing = isFollowing
            })
        });
        this.AddFollowStub(new StubFollowService
        {
            FollowHandler = userId =>
            {
                followCalls.Add(userId);
                isFollowing = true;
                return Task.CompletedTask;
            }
        });
        this.AddConversationStub();
        this.AddPostStub();

        var cut = RenderComponent<PublicProfile>(parameters => parameters.Add(p => p.UserId, "target-user"));
        cut.WaitForElement("[data-testid='profile-follow-toggle']");

        cut.Find("[data-testid='profile-follow-toggle']").Click();

        cut.WaitForAssertion(() =>
        {
            followCalls.Should().ContainSingle().Which.Should().Be("target-user");
            cut.Find("[data-testid='profile-follow-toggle']").TextContent.Should().Contain("Unfollow");
        });
    }

    [Fact]
    public void OwnerView_ShowsEditActionsInsteadOfFollowActions()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddProfileStub(new StubProfileService
        {
            GetByIdHandler = id => Task.FromResult(new UserProfileDto
            {
                UserId = id,
                UserName = "viewer",
                DisplayName = "Viewer Painter"
            })
        });
        this.AddFollowStub();
        this.AddConversationStub();
        this.AddPostStub();

        var cut = RenderComponent<PublicProfile>(parameters => parameters.Add(p => p.UserId, "viewer-user"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Edit profile");
            cut.Markup.Should().Contain("Connections");
            cut.FindAll("[data-testid='profile-follow-toggle']").Should().BeEmpty();
            cut.FindAll("[data-testid='profile-message']").Should().BeEmpty();
        });
    }
}
