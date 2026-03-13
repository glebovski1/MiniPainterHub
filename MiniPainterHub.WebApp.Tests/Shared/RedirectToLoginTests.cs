using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.WebApp.Shared;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class RedirectToLoginTests : TestContext
{
    [Fact]
    public void WhenUnauthenticated_RedirectsToLoginWithReturnUrl()
    {
        var auth = this.AddTestAuthorization();
        auth.SetNotAuthorized();
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/posts/new");

        RenderComponent<RedirectToLogin>();

        nav.Uri.Should().Be("http://localhost/login?returnUrl=http%3A%2F%2Flocalhost%2Fposts%2Fnew");
    }

    [Fact]
    public void WhenAuthenticated_DoesNotRedirectAndShowsAccessDenied()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("viewer");
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/admin/suspensions");

        var cut = RenderComponent<RedirectToLogin>();

        cut.WaitForAssertion(() =>
        {
            nav.Uri.Should().Be("http://localhost/admin/suspensions");
            cut.Find("[data-testid='access-denied-message']").TextContent.Should().Contain("do not have access");
        });
    }
}
