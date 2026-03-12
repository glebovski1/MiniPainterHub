using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class RegistrationTests : TestContext
{
    [Fact]
    public void RendersRegistrationForm()
    {
        this.AddAuthStub();

        var cut = RenderComponent<Registration>();

        cut.Find("#reg-username").Should().NotBeNull();
        cut.Find("#reg-email").Should().NotBeNull();
        cut.Find("#reg-password").Should().NotBeNull();
        cut.Find("button[type='submit']").TextContent.Should().Contain("Create Account");
    }

    [Fact]
    public async Task Submit_WhenRegisterSucceeds_NavigatesToLogin()
    {
        RegisterDto? captured = null;
        this.AddAuthStub(new StubAuthService
        {
            RegisterHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(true);
            }
        });

        var cut = RenderComponent<Registration>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("#reg-username").Change("new-user");
        cut.Find("#reg-email").Change("new-user@example.com");
        cut.Find("#reg-password").Change("ValidPass123!");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() => nav.Uri.Should().Be("http://localhost/login"));
        captured.Should().NotBeNull();
        captured!.UserName.Should().Be("new-user");
        captured.Email.Should().Be("new-user@example.com");
        captured.Password.Should().Be("ValidPass123!");
    }

    [Fact]
    public async Task Submit_WhenRegisterFails_ShowsError()
    {
        this.AddAuthStub(new StubAuthService
        {
            RegisterHandler = _ => Task.FromResult(false)
        });

        var cut = RenderComponent<Registration>();

        cut.Find("#reg-username").Change("new-user");
        cut.Find("#reg-email").Change("new-user@example.com");
        cut.Find("#reg-password").Change("bad");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Registration failed."));
    }

    [Fact]
    public async Task Submit_WhenEmailMissing_DoesNotCallRegister()
    {
        var registerCalls = 0;
        this.AddAuthStub(new StubAuthService
        {
            RegisterHandler = _ =>
            {
                registerCalls++;
                return Task.FromResult(true);
            }
        });

        var cut = RenderComponent<Registration>();

        cut.Find("#reg-username").Change("new-user");
        cut.Find("#reg-password").Change("ValidPass123!");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            registerCalls.Should().Be(0);
            cut.Markup.Should().Contain("The Email field is required.");
        });
    }
}
