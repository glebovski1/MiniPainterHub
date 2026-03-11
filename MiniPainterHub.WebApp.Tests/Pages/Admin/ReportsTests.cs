using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Admin;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class ReportsTests : TestContext
{
    [Fact]
    public void LoadsQueueAndResolveActionCallsService()
    {
        long? resolvedReportId = null;
        ResolveReportRequestDto? resolvedRequest = null;

        this.AddReportStub(new StubReportService
        {
            GetQueueHandler = query => Task.FromResult(new MiniPainterHub.WebApp.Services.Http.ApiResult<PagedResult<ReportQueueItemDto>?>(true, HttpStatusCode.OK, new PagedResult<ReportQueueItemDto>
            {
                Items = new[]
                {
                    new ReportQueueItemDto
                    {
                        Id = 5,
                        CreatedUtc = DateTime.UtcNow,
                        Status = ReportStatuses.Open,
                        TargetType = ReportTargetTypes.Post,
                        TargetId = "10",
                        ReasonCode = ReportReasonCodes.Spam,
                        ReporterUserId = "viewer",
                        ReporterUserName = "Viewer",
                        TargetSummary = "Target post",
                        TargetUrl = "/posts/10"
                    }
                },
                PageNumber = query.Page,
                PageSize = query.PageSize,
                TotalCount = 1
            })),
            ResolveHandler = (reportId, request) =>
            {
                resolvedReportId = reportId;
                resolvedRequest = request;
                return Task.FromResult(true);
            }
        });

        var cut = RenderComponent<Reports>();

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='reports-row']").Should().HaveCount(1));

        cut.Find("[data-testid='report-resolution-note']").Change("Needs moderator review");
        cut.Find("[data-testid='report-resolve-reviewed']").Click();

        cut.WaitForAssertion(() =>
        {
            resolvedReportId.Should().Be(5);
            resolvedRequest.Should().NotBeNull();
            resolvedRequest!.Status.Should().Be(ReportStatuses.Reviewed);
            resolvedRequest.ResolutionNote.Should().Be("Needs moderator review");
        });
    }
}
