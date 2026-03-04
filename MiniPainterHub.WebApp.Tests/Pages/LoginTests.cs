using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class LoginTests : TestContext
{
    [Fact]
    public void RendersLoginFormWithStableSelectors()
    {
        this.AddAuthStub(new StubAuthService { LoginHandler = _ => Task.FromResult(false) });

        var cut = RenderComponent<Login>();

        cut.Find("[data-testid='login-form']").Should().NotBeNull();
        cut.Find("[data-testid='login-username']").Should().NotBeNull();
        cut.Find("[data-testid='login-password']").Should().NotBeNull();
        cut.Find("[data-testid='login-submit']").Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_WhenAuthFails_ShowsUserFacingError()
    {
        this.AddAuthStub(new StubAuthService { LoginHandler = _ => Task.FromResult(false) });
        var cut = RenderComponent<Login>();

        cut.Find("[data-testid='login-username']").Change("user");
        cut.Find("[data-testid='login-password']").Change("wrong-password");
        await cut.Find("[data-testid='login-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='login-error']").TextContent
                .Should().Contain("Invalid username or password."));
    }


    [Fact]
    public async Task Submit_WhenAuthSucceedsAndReturnUrlProvided_NavigatesToReturnUrl()
    {
        this.AddAuthStub(new StubAuthService { LoginHandler = _ => Task.FromResult(true) });
        var nav = Services.GetRequiredService<NavigationManager>();
        var loginWithReturnUrl = nav.GetUriWithQueryParameter("returnUrl", "/posts/my");
        nav.NavigateTo(loginWithReturnUrl);
        var cut = RenderComponent<Login>();

        cut.Find("[data-testid='login-username']").Change("user");
        cut.Find("[data-testid='login-password']").Change("User123!");
        await cut.Find("[data-testid='login-form']").SubmitAsync();

        cut.WaitForAssertion(() => nav.Uri.Should().Be("http://localhost/posts/my"));
    }
    [Fact]
    public async Task Submit_WhenAuthSucceeds_NavigatesToHome()
    {
        this.AddAuthStub(new StubAuthService { LoginHandler = _ => Task.FromResult(true) });
        var cut = RenderComponent<Login>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("[data-testid='login-username']").Change("user");
        cut.Find("[data-testid='login-password']").Change("User123!");
        await cut.Find("[data-testid='login-form']").SubmitAsync();

        cut.WaitForAssertion(() => nav.Uri.Should().Be("http://localhost/"));
    }
}
