using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Users;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Users;

public class PublicProfileTests : TestContext
{
    [Fact]
    public void WhenUnauthenticated_RendersPublicProfile_WithoutAdminActions()
    {
        var auth = this.AddTestAuthorization();
        auth.SetNotAuthorized();

        this.AddProfileStub(new StubProfileService
        {
            GetByIdHandler = id => Task.FromResult(new UserProfileDto
            {
                UserId = id,
                DisplayName = "Target",
                Bio = "Bio"
            })
        });
        this.AddModerationStub();

        var cut = RenderWithAuth("target-user");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='public-profile-name']").TextContent.Should().Be("Target");
            cut.FindAll("[data-testid='public-profile-suspend']").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task WhenAdmin_CanSuspendUser()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");

        this.AddProfileStub(new StubProfileService
        {
            GetByIdHandler = id => Task.FromResult(new UserProfileDto
            {
                UserId = id,
                DisplayName = "Target"
            })
        });

        var suspendCalled = false;
        string? suspendUserId = null;
        this.AddModerationStub(new StubModerationService
        {
            SearchUsersHandler = (query, _) => Task.FromResult(
                new ApiResult<IReadOnlyList<ModerationUserLookupDto>?>(true, HttpStatusCode.OK, new List<ModerationUserLookupDto>
                {
                    new ModerationUserLookupDto { UserId = query ?? "target-user", IsSuspended = false, Roles = new[] { "User" } }
                })),
            SuspendUserHandler = (userId, _) =>
            {
                suspendCalled = true;
                suspendUserId = userId;
                return Task.FromResult(true);
            }
        });

        var cut = RenderWithAuth("target-user");
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='public-profile-suspend']").Should().HaveCount(1));

        await cut.Find("[data-testid='public-profile-suspend']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            suspendCalled.Should().BeTrue();
            suspendUserId.Should().Be("target-user");
            cut.Find("[data-testid='public-profile-action-result']").TextContent.Should().Contain("suspended");
        });
    }

    private IRenderedFragment RenderWithAuth(string userId)
    {
        return Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<PublicProfile>(0);
                childBuilder.AddAttribute(1, nameof(PublicProfile.UserId), userId);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }
}
