using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Shared;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Shared;

public class ReportActionTests : TestContext
{
    [Fact]
    public void Submit_WithOtherReasonAndNoDetails_ShowsValidationError()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");

        var called = false;
        this.AddReportStub(new StubReportService
        {
            ReportPostHandler = (_, _) =>
            {
                called = true;
                return Task.FromResult(true);
            }
        });

        var cut = RenderComponent<ReportAction>(parameters => parameters
            .Add(p => p.TargetType, ReportTargetTypes.Post)
            .Add(p => p.TargetId, "12")
            .Add(p => p.OwnerUserId, "author-user")
            .Add(p => p.TestIdPrefix, "report"));

        cut.Find("[data-testid='report-toggle']").Click();
        cut.Find("[data-testid='report-reason']").Change(ReportReasonCodes.Other);
        cut.Find("[data-testid='report-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            called.Should().BeFalse();
            cut.Find("[data-testid='report-error']").TextContent.Should().Contain("Details are required");
        });
    }

    [Fact]
    public void Submit_WhenAuthenticatedAndNotOwner_CallsReportServiceAndShowsSuccess()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");

        int? capturedPostId = null;
        CreateReportRequestDto? capturedRequest = null;
        this.AddReportStub(new StubReportService
        {
            ReportPostHandler = (postId, request) =>
            {
                capturedPostId = postId;
                capturedRequest = request;
                return Task.FromResult(true);
            }
        });

        var cut = RenderComponent<ReportAction>(parameters => parameters
            .Add(p => p.TargetType, ReportTargetTypes.Post)
            .Add(p => p.TargetId, "42")
            .Add(p => p.OwnerUserId, "author-user")
            .Add(p => p.TestIdPrefix, "report"));

        cut.Find("[data-testid='report-toggle']").Click();
        cut.Find("[data-testid='report-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            capturedPostId.Should().Be(42);
            capturedRequest.Should().NotBeNull();
            capturedRequest!.ReasonCode.Should().Be(ReportReasonCodes.Spam);
            cut.Find("[data-testid='report-result']").TextContent.Should().Contain("Report submitted");
        });
    }

    [Fact]
    public void Submit_WhenServiceThrows_ShowsRecoverableError()
    {
        this.SetAuthenticatedUser("viewer-user", "viewer");

        this.AddReportStub(new StubReportService
        {
            ReportPostHandler = (_, _) => throw new System.InvalidOperationException("boom")
        });

        var cut = RenderComponent<ReportAction>(parameters => parameters
            .Add(p => p.TargetType, ReportTargetTypes.Post)
            .Add(p => p.TargetId, "42")
            .Add(p => p.OwnerUserId, "author-user")
            .Add(p => p.TestIdPrefix, "report"));

        cut.Find("[data-testid='report-toggle']").Click();
        cut.Find("[data-testid='report-submit']").Click();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='report-error']").TextContent.ToLowerInvariant().Should().Contain("couldn't submit the report"));
    }
}
