using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Services.Auth;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class EmailConfirmationPagesTests : TestContext
{
    [Fact]
    public void ConfirmEmail_WhenTokenSucceeds_ShowsLoginAction()
    {
        this.AddAuthStub(new StubAuthService
        {
            ConfirmEmailHandler = _ => Task.FromResult(EmailConfirmationOutcome.Success)
        });

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/confirm-email?userId=user-1&code=token&returnUrl=%2Fsupport");
        var cut = RenderComponent<ConfirmEmail>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='confirm-email-success']").TextContent.Should().Contain("Email confirmed");
            cut.Markup.Should().Contain("/login?returnUrl=%2Fsupport");
        });
    }

    [Fact]
    public void ConfirmEmail_WhenTokenIsInvalid_ShowsResendAction()
    {
        this.AddAuthStub(new StubAuthService
        {
            ConfirmEmailHandler = _ => Task.FromResult(EmailConfirmationOutcome.InvalidOrExpired)
        });

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/confirm-email?userId=user-1&code=bad-token");
        var cut = RenderComponent<ConfirmEmail>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='confirm-email-invalid']").Should().NotBeNull();
            cut.Markup.Should().Contain("/resend-confirmation");
        });
    }

    [Fact]
    public async Task ResendConfirmation_WhenAccepted_ShowsEnumerationSafeMessage()
    {
        this.AddAuthStub();
        var cut = RenderComponent<ResendEmailConfirmation>();

        cut.Find("#resend-email").Change("unknown@example.test");
        await cut.Find("form").SubmitAsync();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='resend-confirmation-success']").TextContent.Should().Contain("If an unconfirmed account matches"));
    }
}
