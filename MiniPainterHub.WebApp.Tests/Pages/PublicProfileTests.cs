using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using System.Collections.Generic;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class PublicProfileTests : TestContext
{
    [Fact]
    public void PublicProfile_ShowsUpToThreeRecentProjectsBeforeGallery()
    {
        this.AddTestAuthorization().SetNotAuthorized();
        this.AddProfileStub(new StubProfileService
        {
            GetPublicByIdHandler = id => Task.FromResult(new PublicUserProfileDto
            {
                UserId = id,
                UserName = "painter",
                DisplayName = "Project Painter"
            })
        });
        this.AddHobbyProjectStub(new StubHobbyProjectService
        {
            GetByOwnerHandler = (ownerId, query) => Task.FromResult(
                new MiniPainterHub.WebApp.Services.Http.ApiResult<PagedResult<HobbyProjectSummaryDto>?>(true, HttpStatusCode.OK, new PagedResult<HobbyProjectSummaryDto>
                {
                    Items = new[]
                    {
                        new HobbyProjectSummaryDto
                        {
                            Id = 7,
                            OwnerUserId = ownerId,
                            OwnerUserName = "painter",
                            OwnerDisplayName = "Project Painter",
                            Title = "Winter army",
                            Description = "A cold-weather force.",
                            Kind = HobbyProjectKinds.Army,
                            Status = HobbyProjectStatuses.InProgress,
                            EntryCount = 2,
                            IsPublic = true,
                            UpdatedUtc = DateTime.UtcNow
                        }
                    },
                    PageNumber = query.PageNumber,
                    PageSize = query.PageSize,
                    TotalCount = 1
                }))
        });
        this.AddFollowStub();
        this.AddConversationStub();
        this.AddPostStub();

        var cut = RenderComponent<PublicProfile>(parameters => parameters.Add(p => p.UserId, "target-user"));

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='profile-projects']").TextContent.Should().Contain("Winter army");
            cut.Find("[data-testid='profile-projects-all']").GetAttribute("href").Should().Contain("owner=target-user");
        });
    }

    [Fact]
    public void WhenViewingAnotherUser_RendersFollowAndMessageActions()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddProfileStub(new StubProfileService
        {
            GetPublicByIdHandler = id => Task.FromResult(new PublicUserProfileDto
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
            GetPublicByIdHandler = id => Task.FromResult(new PublicUserProfileDto
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
            GetPublicByIdHandler = id => Task.FromResult(new PublicUserProfileDto
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
            GetPublicByIdHandler = id => Task.FromResult(new PublicUserProfileDto
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
