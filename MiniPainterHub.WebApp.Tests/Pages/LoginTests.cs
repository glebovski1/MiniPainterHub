using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Common.Auth;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Services.Auth;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class LoginTests : TestContext
{
    public LoginTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void RendersLoginFormWithStableSelectors()
    {
        this.AddAuthStub(new StubAuthService { LoginHandler = _ => Task.FromResult(LoginOutcome.Unavailable) });

        var cut = RenderComponent<Login>();

        cut.Find("[data-testid='login-form']").Should().NotBeNull();
        cut.Find("[data-testid='login-username']").Should().NotBeNull();
        cut.Find("[data-testid='login-password']").Should().NotBeNull();
        cut.Find("[data-testid='login-submit']").Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_WhenCredentialsAreEmpty_ShowsOneFocusedValidationAlertWithoutCallingAuth()
    {
        var loginCalls = 0;
        this.AddAuthStub(new StubAuthService
        {
            LoginHandler = _ =>
            {
                loginCalls++;
                return Task.FromResult(LoginOutcome.Success);
            }
        });
        var cut = RenderComponent<Login>();

        await cut.Find("[data-testid='login-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[role='alert']").Should().ContainSingle();
            var alert = cut.Find("[data-testid='login-error']");
            alert.TextContent.Should().Contain("Enter your username and password.");
            alert.GetAttribute("tabindex").Should().Be("-1");
            loginCalls.Should().Be(0);
            JSInterop.Invocations.Should().Contain(invocation =>
                invocation.Identifier == "Blazor._internal.domWrapper.focus");
        });
    }

    [Fact]
    public async Task InputEvents_UpdateCredentialsBeforeSuccessfulSubmit()
    {
        LoginDto? submitted = null;
        this.AddAuthStub(new StubAuthService
        {
            LoginHandler = dto =>
            {
                submitted = new LoginDto { UserName = dto.UserName, Password = dto.Password };
                return Task.FromResult(LoginOutcome.Success);
            }
        });
        var cut = RenderComponent<Login>();
        var nav = Services.GetRequiredService<NavigationManager>();

        cut.Find("[data-testid='login-username']").Input("artist");
        cut.Find("[data-testid='login-password']").Input("User123!");
        await cut.Find("[data-testid='login-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            submitted.Should().NotBeNull();
            submitted!.UserName.Should().Be("artist");
            submitted.Password.Should().Be("User123!");
            nav.Uri.Should().Be("http://localhost/");
        });
    }

    [Theory]
    [InlineData(LoginOutcome.InvalidCredentials, "Invalid username or password.")]
    [InlineData(LoginOutcome.ValidationFailure, "Check your username and password, then try again.")]
    [InlineData(LoginOutcome.Forbidden, "This account cannot sign in right now.")]
    [InlineData(LoginOutcome.RateLimited, "Too many sign-in attempts.")]
    [InlineData(LoginOutcome.Unavailable, "Sign-in is unavailable right now.")]
    public async Task Submit_WhenAuthDoesNotSucceed_ShowsOneOutcomeSpecificAlert(
        LoginOutcome outcome,
        string expectedMessage)
    {
        this.AddAuthStub(new StubAuthService { LoginHandler = _ => Task.FromResult(outcome) });
        var cut = RenderComponent<Login>();

        cut.Find("[data-testid='login-username']").Input("artist");
        cut.Find("[data-testid='login-password']").Input("User123!");
        await cut.Find("[data-testid='login-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[role='alert']").Should().ContainSingle();
            cut.Find("[data-testid='login-error']").TextContent.Should().Contain(expectedMessage);
        });
    }

    [Fact]
    public async Task Submit_WhileAuthIsPending_DisablesButtonAndMarksFormBusy()
    {
        var completion = new TaskCompletionSource<LoginOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.AddAuthStub(new StubAuthService { LoginHandler = _ => completion.Task });
        var cut = RenderComponent<Login>();

        cut.Find("[data-testid='login-username']").Input("artist");
        cut.Find("[data-testid='login-password']").Input("User123!");
        var submitTask = cut.Find("[data-testid='login-form']").SubmitAsync();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='login-submit']").HasAttribute("disabled").Should().BeTrue();
            cut.Find("[data-testid='login-submit']").TextContent.Should().Contain("Signing in...");
            cut.Find("[data-testid='login-form']").GetAttribute("aria-busy").Should().Be("true");
        });

        completion.SetResult(LoginOutcome.Unavailable);
        await submitTask;

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='login-submit']").HasAttribute("disabled").Should().BeFalse();
            cut.Find("[data-testid='login-form']").GetAttribute("aria-busy").Should().Be("false");
        });
    }

    [Fact]
    public async Task InputAfterFailure_ClearsTheStaleAlert()
    {
        this.AddAuthStub(new StubAuthService { LoginHandler = _ => Task.FromResult(LoginOutcome.InvalidCredentials) });
        var cut = RenderComponent<Login>();

        cut.Find("[data-testid='login-username']").Input("artist");
        cut.Find("[data-testid='login-password']").Input("wrong-password");
        await cut.Find("[data-testid='login-form']").SubmitAsync();
        cut.WaitForElement("[data-testid='login-error']");

        cut.Find("[data-testid='login-password']").Input("User123!");

        cut.FindAll("[data-testid='login-error']").Should().BeEmpty();
    }
}
