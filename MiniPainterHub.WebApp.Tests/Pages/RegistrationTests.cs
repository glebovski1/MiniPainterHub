using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Services.Auth;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class RegistrationTests : BunitContext
{
    [Fact]
    public void RendersRegistrationForm()
    {
        this.AddAuthStub();

        var cut = Render<Registration>();

        cut.Find("#reg-username").Should().NotBeNull();
        cut.Find("#reg-email").Should().NotBeNull();
        cut.Find("#reg-password").Should().NotBeNull();
        cut.Find("button[type='submit']").TextContent.Should().Contain("Create Account");
    }

    [Fact]
    public void WhenGoogleIsEnabled_RendersProviderAction()
    {
        this.AddAuthStub(new StubAuthService
        {
            GetProvidersHandler = () => Task.FromResult(new AuthProvidersDto
            {
                Google = new AuthProviderDto { Name = "Google", DisplayName = "Google", Enabled = true }
            })
        });

        var cut = Render<Registration>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='google-signin-link']").Should().NotBeNull());
    }

    [Fact]
    public void WhenGoogleAndDiscordAreEnabled_RendersBothActionsAndOnePasswordDivider()
    {
        this.AddAuthStub(new StubAuthService
        {
            GetProvidersHandler = () => Task.FromResult(new AuthProvidersDto
            {
                Google = new AuthProviderDto { Name = "Google", DisplayName = "Google", Enabled = true },
                Discord = new AuthProviderDto { Name = "Discord", DisplayName = "Discord", Enabled = true }
            })
        });

        var cut = Render<Registration>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='google-signin-link']").Should().NotBeNull();
            cut.Find("[data-testid='discord-signin-link']").Should().NotBeNull();
            cut.FindAll(".external-signin__divider").Should().ContainSingle();
        });
    }

    [Fact]
    public async Task Submit_WhenConfirmationIsSent_ShowsCheckEmailStateAndPassesSafeReturnUrl()
    {
        RegisterDto? captured = null;
        this.AddAuthStub(new StubAuthService
        {
            RegisterHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(RegistrationOutcome.ConfirmationSent);
            }
        });

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameter("returnUrl", "/support"));
        var cut = Render<Registration>();

        cut.Find("#reg-username").Change("new-user");
        cut.Find("#reg-email").Change("new-user@example.com");
        cut.Find("#reg-password").Change("ValidPass123!");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() => cut.Find("[data-testid='register-confirmation-sent']").TextContent.Should().Contain("new-user@example.com"));
        captured.Should().NotBeNull();
        captured!.UserName.Should().Be("new-user");
        captured.Email.Should().Be("new-user@example.com");
        captured.Password.Should().Be("ValidPass123!");
        captured.ReturnUrl.Should().Be("/support");
    }

    [Fact]
    public async Task Submit_WhenRegisterFails_ShowsError()
    {
        this.AddAuthStub(new StubAuthService
        {
            RegisterHandler = _ => Task.FromResult(RegistrationOutcome.ValidationFailure)
        });

        var cut = Render<Registration>();

        cut.Find("#reg-username").Change("new-user");
        cut.Find("#reg-email").Change("new-user@example.com");
        cut.Find("#reg-password").Change("bad");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Check your account details and try again."));
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
                return Task.FromResult(RegistrationOutcome.ConfirmationSent);
            }
        });

        var cut = Render<Registration>();

        cut.Find("#reg-username").Change("new-user");
        cut.Find("#reg-password").Change("ValidPass123!");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            registerCalls.Should().Be(0);
            cut.Markup.Should().Contain("The Email field is required.");
        });
    }


    [Fact]
    public async Task Submit_WhenDeliveryFails_ShowsResendRecoveryWithoutRecreatingAccount()
    {
        this.AddAuthStub(new StubAuthService
        {
            RegisterHandler = _ => Task.FromResult(RegistrationOutcome.ConfirmationPendingDelivery)
        });

        var cut = Render<Registration>();
        cut.Find("#reg-username").Change("new-user");
        cut.Find("#reg-email").Change("new-user@example.com");
        cut.Find("#reg-password").Change("ValidPass123!");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='register-confirmation-pending']").TextContent.Should().Contain("Account created");
            cut.Markup.Should().Contain("/resend-confirmation");
        });
    }

    [Fact]
    public async Task Submit_WhenReturnUrlIsExternal_ReplacesItWithApplicationRoot()
    {
        RegisterDto? captured = null;
        this.AddAuthStub(new StubAuthService
        {
            RegisterHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(RegistrationOutcome.ConfirmationSent);
            }
        });

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameter("returnUrl", "https://evil.example/steal"));
        var cut = Render<Registration>();
        cut.Find("#reg-username").Change("new-user");
        cut.Find("#reg-email").Change("new-user@example.com");
        cut.Find("#reg-password").Change("ValidPass123!");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() => captured.Should().NotBeNull());
        captured!.ReturnUrl.Should().Be("/");
    }
}
