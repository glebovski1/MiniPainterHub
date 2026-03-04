using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.WebApp.Pages.Admin;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class ModerationDashboardTests : TestContext
{
    [Fact]
    public void RendersModerationDashboardInputs()
    {
        this.AddModerationStub();

        var cut = RenderComponent<ModerationDashboard>();

        cut.Find("[data-testid='mod-post-id']").Should().NotBeNull();
        cut.Find("[data-testid='mod-post-action']").Should().NotBeNull();
        cut.Find("[data-testid='mod-comment-id']").Should().NotBeNull();
        cut.Find("[data-testid='mod-comment-action']").Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitPostHide_WhenValid_CallsServiceAndShowsSuccessMessage()
    {
        var called = false;
        var capturedId = 0;
        string? capturedReason = null;

        this.AddModerationStub(new StubModerationService
        {
            HidePostHandler = (id, request) =>
            {
                called = true;
                capturedId = id;
                capturedReason = request.Reason;
                return Task.FromResult(true);
            }
        });

        var cut = RenderComponent<ModerationDashboard>();
        cut.Find("[data-testid='mod-post-id']").Change("301");
        cut.Find("[data-testid='mod-post-action']").Change("hide");
        cut.Find("[data-testid='mod-post-reason']").Change("policy violation");
        await cut.Find("[data-testid='mod-post-submit']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            called.Should().BeTrue();
            capturedId.Should().Be(301);
            capturedReason.Should().Be("policy violation");
            cut.Find("[data-testid='mod-post-result']").TextContent.Should().Contain("Post 301 hidden.");
        });
    }

    [Fact]
    public async Task SubmitCommentRestore_WhenIdInvalid_ShowsValidationAndSkipsServiceCall()
    {
        var called = false;
        this.AddModerationStub(new StubModerationService
        {
            RestoreCommentHandler = (_, _) =>
            {
                called = true;
                return Task.FromResult(true);
            }
        });

        var cut = RenderComponent<ModerationDashboard>();
        cut.Find("[data-testid='mod-comment-id']").Change("0");
        cut.Find("[data-testid='mod-comment-action']").Change("restore");
        await cut.Find("[data-testid='mod-comment-submit']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            called.Should().BeFalse();
            cut.Find("[data-testid='mod-comment-error']").TextContent.Should().Contain("greater than 0");
        });
    }
}
