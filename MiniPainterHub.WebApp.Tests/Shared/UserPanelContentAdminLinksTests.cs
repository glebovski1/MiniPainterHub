using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MiniPainterHub.WebApp.Shared;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class UserPanelContentAdminLinksTests : TestContext
{
    [Fact]
    public void WhenUserRole_IsNotAdminOrModerator_DoesNotRenderAdminLinks()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("user");
        auth.SetRoles("User");

        var cut = RenderPanel();

        cut.FindAll("[data-testid='admin-nav-moderation']").Should().BeEmpty();
        cut.FindAll("[data-testid='admin-nav-audit']").Should().BeEmpty();
        cut.FindAll("[data-testid='admin-nav-suspensions']").Should().BeEmpty();
    }

    [Fact]
    public void WhenModeratorRole_RendersModerationAndAuditOnly()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("moderator");
        auth.SetRoles("Moderator");

        var cut = RenderPanel();

        cut.FindAll("[data-testid='admin-nav-moderation']").Should().HaveCount(1);
        cut.FindAll("[data-testid='admin-nav-audit']").Should().HaveCount(1);
        cut.FindAll("[data-testid='admin-nav-suspensions']").Should().BeEmpty();
    }

    [Fact]
    public void WhenAdminRole_RendersAllAdminLinks()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");

        var cut = RenderPanel();

        cut.FindAll("[data-testid='admin-nav-moderation']").Should().HaveCount(1);
        cut.FindAll("[data-testid='admin-nav-audit']").Should().HaveCount(1);
        cut.FindAll("[data-testid='admin-nav-suspensions']").Should().HaveCount(1);
    }

    private IRenderedFragment RenderPanel()
    {
        return Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<UserPanelContent>(0);
                childBuilder.AddAttribute(1, nameof(UserPanelContent.ShowPanelTitle), true);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }
}
