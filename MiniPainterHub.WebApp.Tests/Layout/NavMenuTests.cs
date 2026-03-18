using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MiniPainterHub.WebApp.Layout;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
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

    [Fact]
    public void LatestLink_UsesExactRouteMatching()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddAuthStub();
        Services.AddSingleton(new UserPanelState());
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.NavigateTo("https://localhost/posts/all");

        var cut = RenderComponent<NavMenu>();
        var latest = cut.FindAll("a").Single(link => link.TextContent.Contains("Latest"));
        var explore = cut.FindAll("a").Single(link => link.TextContent.Contains("Explore"));

        latest.ClassList.Should().NotContain("active");
        explore.ClassList.Should().Contain("active");
    }
}
