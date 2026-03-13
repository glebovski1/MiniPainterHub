using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.WebApp.Layout;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Layout;

public class MainLayoutTests : TestContext
{
    [Fact]
    public void WhenDesktopPanelIsCollapsed_KeepsSidebarInLayoutAndMarksItInert()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");
        this.AddAuthStub();
        this.AddConversationStub();
        Services.AddSingleton(new UserPanelState());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var state = Services.GetRequiredService<UserPanelState>();
        var cut = RenderComponent<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddMarkupContent(0, "<p>Body</p>")));

        state.ToggleDesktopCollapsed();

        cut.WaitForAssertion(() =>
        {
            var sidebar = cut.Find("aside.dashboard-sidebar-column");
            sidebar.ClassList.Should().Contain("is-collapsed");
            sidebar.HasAttribute("hidden").Should().BeFalse();
            sidebar.HasAttribute("inert").Should().BeTrue();
            sidebar.GetAttribute("aria-hidden").Should().Be("true");
        });
    }

    [Fact]
    public void WhenAdminCollapsesDesktopPanel_KeepsAdminLinksInCollapsedSidebarMarkup()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("admin");
        auth.SetRoles("Admin");
        this.AddAuthStub();
        this.AddConversationStub();
        Services.AddSingleton(new UserPanelState());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var state = Services.GetRequiredService<UserPanelState>();
        var cut = RenderComponent<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, builder => builder.AddMarkupContent(0, "<p>Admin body</p>")));

        cut.Find("aside.dashboard-sidebar-column [data-testid='admin-nav-suspensions']").Should().NotBeNull();

        state.ToggleDesktopCollapsed();

        cut.WaitForAssertion(() =>
        {
            var sidebar = cut.Find("aside.dashboard-sidebar-column");
            sidebar.ClassList.Should().Contain("is-collapsed");
            sidebar.QuerySelectorAll("[data-testid='admin-nav-suspensions']").Should().HaveCount(1);
            sidebar.HasAttribute("inert").Should().BeTrue();
        });
    }
}
