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
    public void WhenAuthenticated_RendersGlobalNavigationAndLogout()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddAuthStub();
        Services.AddSingleton(new UserPanelState());

        var cut = RenderComponent<NavMenu>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Latest");
            cut.Markup.Should().Contain("Explore");
            cut.Markup.Should().Contain("Top posts");
            cut.Markup.Should().Contain("Highlights");
            cut.Markup.Should().NotContain("Following");
            cut.Markup.Should().NotContain("Messages");
            cut.FindAll("[data-testid='nav-login']").Should().BeEmpty();
            cut.Find("[data-testid='nav-search-input']").Should().NotBeNull();
            cut.Find("[data-testid='nav-logout']").Should().NotBeNull();
        });
    }

    [Fact]
    public void WhenAnonymous_RendersGlobalNavigationAndAnonymousSessionLinks()
    {
        var auth = this.AddTestAuthorization();
        auth.SetNotAuthorized();
        this.AddAuthStub();
        Services.AddSingleton(new UserPanelState());

        var cut = RenderComponent<NavMenu>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Latest");
            cut.Markup.Should().Contain("Explore");
            cut.Markup.Should().Contain("Top posts");
            cut.Markup.Should().Contain("Highlights");
            cut.Markup.Should().NotContain("Following");
            cut.Markup.Should().NotContain("Messages");
            cut.Find("[data-testid='nav-login']").Should().NotBeNull();
            cut.Find("[data-testid='nav-search-submit']").Should().NotBeNull();
            cut.FindAll("[data-testid='nav-logout']").Should().BeEmpty();
        });
    }
}
