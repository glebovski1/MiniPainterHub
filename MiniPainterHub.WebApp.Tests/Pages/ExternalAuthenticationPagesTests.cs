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

public class ExternalAuthenticationPagesTests : BunitContext
{
    [Fact]
    public void Callback_WhenRegistrationIsRequired_PreservesFlowInMemoryAndNavigates()
    {
        this.AddAuthStub(new StubAuthService
        {
            ExchangeExternalHandler = () => Task.FromResult(new ExternalAuthClientResult(
                ExternalAuthClientOutcome.RegistrationRequired,
                "painter@example.test",
                "painter",
                "/support",
                ExternalAuthProviderNames.Discord))
        });
        Services.AddSingleton(new ExternalAuthFlowState());
        var nav = Services.GetRequiredService<NavigationManager>();

        Render<ExternalAuthCallback>();

        nav.Uri.Should().Be("http://localhost/auth/external/complete-registration");
        var state = Services.GetRequiredService<ExternalAuthFlowState>();
        state.RegistrationPending.Should().BeTrue();
        state.Email.Should().Be("painter@example.test");
        state.ReturnUrl.Should().Be("/support");
        state.Provider.Should().Be(ExternalAuthProviderNames.Discord);
    }

    [Fact]
    public void Callback_WhenEmailConflicts_ShowsExplicitNoMergeGuidance()
    {
        this.AddAuthStub(new StubAuthService
        {
            ExchangeExternalHandler = () => Task.FromResult(new ExternalAuthClientResult(ExternalAuthClientOutcome.EmailConflict))
        });
        Services.AddSingleton(new ExternalAuthFlowState());

        var cut = Render<ExternalAuthCallback>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='external-auth-result-title']").TextContent.Should().Contain("Sign in before connecting Google");
            cut.Find("[data-testid='external-auth-result-message']").TextContent.Should().Contain("never merged automatically");
        });
    }

    [Fact]
    public void Callback_WhenDiscordEmailConflicts_UsesDiscordGuidance()
    {
        this.AddAuthStub(new StubAuthService
        {
            ExchangeExternalHandler = () => Task.FromResult(new ExternalAuthClientResult(
                ExternalAuthClientOutcome.EmailConflict,
                Provider: ExternalAuthProviderNames.Discord))
        });
        Services.AddSingleton(new ExternalAuthFlowState());

        var cut = Render<ExternalAuthCallback>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='external-auth-result-title']").TextContent.Should().Contain("connecting Discord");
            cut.Find("[data-testid='external-auth-result-message']").TextContent.Should().Contain("verified Discord email");
        });
    }

    [Fact]
    public void Callback_WhenDiscordIdentityIsUnverified_ShowsRecoveryGuidanceWithoutExchange()
    {
        var exchangeCalls = 0;
        this.AddAuthStub(new StubAuthService
        {
            ExchangeExternalHandler = () =>
            {
                exchangeCalls++;
                return Task.FromResult(new ExternalAuthClientResult(ExternalAuthClientOutcome.Unavailable));
            }
        });
        Services.AddSingleton(new ExternalAuthFlowState());
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/auth/external/callback?provider=Discord&error=unverified");

        var cut = Render<ExternalAuthCallback>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='external-auth-result-title']").TextContent.Should().Contain("Discord could not verify");
            exchangeCalls.Should().Be(0);
        });
    }

    [Fact]
    public async Task Registration_WhenUsernameFails_PreservesDraftAndShowsRetryableError()
    {
        this.AddAuthStub(new StubAuthService
        {
            CompleteExternalRegistrationHandler = _ => Task.FromResult(LoginOutcome.ValidationFailure)
        });
        var state = new ExternalAuthFlowState();
        state.BeginRegistration(new ExternalAuthClientResult(
            ExternalAuthClientOutcome.RegistrationRequired,
            "painter@example.test",
            "suggested",
            "/"));
        Services.AddSingleton(state);

        var cut = Render<ExternalAuthRegistration>();
        cut.Find("[data-testid='external-registration-username']").Change("chosen-name");
        await cut.Find("[data-testid='external-registration-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='external-registration-error']").TextContent.Should().Contain("unavailable or invalid");
            cut.Find("[data-testid='external-registration-username']").GetAttribute("value").Should().Be("chosen-name");
            state.RegistrationPending.Should().BeTrue();
        });
    }

    [Fact]
    public void SignInMethods_GoogleOnlyAccountRequiresPasswordBeforeDisconnect()
    {
        this.AddAuthStub(new StubAuthService
        {
            GetProvidersHandler = () => Task.FromResult(new AuthProvidersDto
            {
                Google = new AuthProviderDto { Name = "Google", DisplayName = "Google", Enabled = true }
            }),
            GetSignInMethodsHandler = () => Task.FromResult<SignInMethodsDto?>(new SignInMethodsDto
            {
                HasPassword = false,
                GoogleConnected = true,
                CanDisconnectGoogle = false
            })
        });

        var cut = Render<SignInMethods>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='set-password-form']").Should().NotBeNull();
            cut.Find("[data-testid='google-disconnect-blocked']").TextContent.Should().Contain("only sign-in method");
            cut.FindAll("[data-testid='disconnect-google']").Should().BeEmpty();
        });
    }

    [Fact]
    public void SignInMethods_DisconnectConfirmationUpdatesConnectedState()
    {
        this.AddAuthStub(new StubAuthService
        {
            GetSignInMethodsHandler = () => Task.FromResult<SignInMethodsDto?>(new SignInMethodsDto
            {
                HasPassword = true,
                GoogleConnected = true,
                CanDisconnectGoogle = true
            }),
            DisconnectExternalHandler = _ => Task.FromResult<SignInMethodsDto?>(new SignInMethodsDto
            {
                HasPassword = true,
                GoogleConnected = false,
                CanDisconnectGoogle = false
            })
        });
        var cut = Render<SignInMethods>();
        cut.WaitForElement("[data-testid='disconnect-google']").Click();

        cut.Find("[data-testid='disconnect-google-confirm']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='sign-in-methods-status']").TextContent.Should().Contain("Google was disconnected");
            cut.Markup.Should().Contain("Not connected");
        });
    }

    [Fact]
    public void SignInMethods_DiscordOnlyAccountRequiresAnotherMethodBeforeDisconnect()
    {
        this.AddAuthStub(new StubAuthService
        {
            GetProvidersHandler = () => Task.FromResult(new AuthProvidersDto
            {
                Discord = new AuthProviderDto { Name = "Discord", DisplayName = "Discord", Enabled = true }
            }),
            GetSignInMethodsHandler = () => Task.FromResult<SignInMethodsDto?>(new SignInMethodsDto
            {
                DiscordConnected = true,
                CanDisconnectDiscord = false
            })
        });

        var cut = Render<SignInMethods>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='discord-disconnect-blocked']").TextContent.Should().Contain("only sign-in method");
            cut.FindAll("[data-testid='disconnect-discord']").Should().BeEmpty();
        });
    }

    [Fact]
    public void SignInMethods_DiscordDisconnectUsesProviderAwareService()
    {
        string? disconnectedProvider = null;
        this.AddAuthStub(new StubAuthService
        {
            GetSignInMethodsHandler = () => Task.FromResult<SignInMethodsDto?>(new SignInMethodsDto
            {
                HasPassword = true,
                DiscordConnected = true,
                CanDisconnectDiscord = true
            }),
            DisconnectExternalHandler = provider =>
            {
                disconnectedProvider = provider;
                return Task.FromResult<SignInMethodsDto?>(new SignInMethodsDto { HasPassword = true });
            }
        });
        var cut = Render<SignInMethods>();
        cut.WaitForElement("[data-testid='disconnect-discord']").Click();

        cut.Find("[data-testid='disconnect-discord-confirm']").Click();

        cut.WaitForAssertion(() =>
        {
            disconnectedProvider.Should().Be(ExternalAuthProviderNames.Discord);
            cut.Find("[data-testid='sign-in-methods-status']").TextContent.Should().Contain("Discord was disconnected");
        });
    }

    [Fact]
    public void Privacy_RendersConfiguredSupportContact()
    {
        this.AddAuthStub(new StubAuthService
        {
            GetProvidersHandler = () => Task.FromResult(new AuthProvidersDto { SupportEmail = "support@example.test" })
        });

        var cut = Render<Privacy>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='privacy-support-email']").TextContent.Should().Contain("support@example.test"));
    }

    [Fact]
    public void Terms_RendersConfiguredSupportContact()
    {
        this.AddAuthStub(new StubAuthService
        {
            GetProvidersHandler = () => Task.FromResult(new AuthProvidersDto { SupportEmail = "support@example.test" })
        });

        var cut = Render<Terms>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='terms-support-email']").TextContent.Should().Contain("support@example.test"));
    }
}
