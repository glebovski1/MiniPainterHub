using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MiniPainterHub.WebApp.Layout;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Layout;

public class NavMenuTests : TestContext
{
    [Fact]
    public void WhenAuthenticated_RendersSocialNavigationLinks()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddAuthStub();
        Services.AddSingleton(new UserPanelState());

        var cut = RenderComponent<NavMenu>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Following");
            cut.Markup.Should().Contain("Messages");
            cut.FindAll("[data-testid='nav-login']").Should().BeEmpty();
            cut.Find("[data-testid='nav-logout']").Should().NotBeNull();
        });
    }

    [Fact]
    public void WhenAnonymous_HidesSocialNavigationLinks()
    {
        var auth = this.AddTestAuthorization();
        auth.SetNotAuthorized();
        this.AddAuthStub();
        Services.AddSingleton(new UserPanelState());

        var cut = RenderComponent<NavMenu>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().NotContain("Following");
            cut.Markup.Should().NotContain("Messages");
            cut.Find("[data-testid='nav-login']").Should().NotBeNull();
            cut.FindAll("[data-testid='nav-logout']").Should().BeEmpty();
        });
    }
}
