using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Services.Interfaces;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class LoginTests : TestContext
{
    [Fact]
    public void RendersLoginFormWithStableSelectors()
    {
        Services.AddSingleton<IAuthService>(new StubAuthService(loginResult: false));

        var cut = RenderComponent<Login>();

        cut.Find("[data-testid='login-form']").Should().NotBeNull();
        cut.Find("[data-testid='login-username']").Should().NotBeNull();
        cut.Find("[data-testid='login-password']").Should().NotBeNull();
        cut.Find("[data-testid='login-submit']").Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_WhenAuthFails_ShowsUserFacingError()
    {
        Services.AddSingleton<IAuthService>(new StubAuthService(loginResult: false));
        var cut = RenderComponent<Login>();

        cut.Find("[data-testid='login-username']").Change("user");
        cut.Find("[data-testid='login-password']").Change("wrong-password");
        await cut.Find("[data-testid='login-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='login-error']").TextContent
                .Should().Contain("Invalid username or password."));
    }

    [Fact]
    public async Task Submit_WhenAuthSucceeds_NavigatesToHome()
    {
        Services.AddSingleton<IAuthService>(new StubAuthService(loginResult: true));
        var cut = RenderComponent<Login>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("[data-testid='login-username']").Change("user");
        cut.Find("[data-testid='login-password']").Change("User123!");
        await cut.Find("[data-testid='login-form']").SubmitAsync();

        cut.WaitForAssertion(() => nav.Uri.Should().Be("http://localhost/"));
    }

    private sealed class StubAuthService : IAuthService
    {
        private readonly bool _loginResult;

        public StubAuthService(bool loginResult)
        {
            _loginResult = loginResult;
        }

        public Task<bool> LoginAsync(LoginDto dto) => Task.FromResult(_loginResult);

        public Task<bool> RegisterAsync(RegisterDto dto) => Task.FromResult(true);

        public Task LogoutAsync() => Task.CompletedTask;
    }
}
