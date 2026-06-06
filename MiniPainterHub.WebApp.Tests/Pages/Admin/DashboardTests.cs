using System;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Admin;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class DashboardTests : TestContext
{
    [Fact]
    public void RendersLiveMetricsWithoutModerationActions()
    {
        this.AddAdminStub(new StubAdminService
        {
            GetDashboardHandler = windowHours => Task.FromResult(new ApiResult<AdminDashboardStatsDto?>(true, HttpStatusCode.OK, new AdminDashboardStatsDto
            {
                GeneratedUtc = DateTime.UtcNow,
                WindowHours = windowHours,
                Metrics = new[]
                {
                    new AdminDashboardMetricDto { Key = "active", Label = "Active sessions", Value = "3", Status = "Normal" },
                    new AdminDashboardMetricDto { Key = "api", Label = "API success", Value = "99.0", Unit = "%", Status = "Normal" }
                },
                Activity = new[]
                {
                    new AdminDashboardActivityPointDto { TimestampUtc = DateTime.UtcNow.AddMinutes(-10), PageViews = 4, ApiRequests = 8, ApiErrors = 0 }
                },
                Health = new[]
                {
                    new AdminDashboardHealthDto { Key = "api", Label = "API", Status = "Healthy", Detail = "Healthy" }
                }
            }))
        });

        var cut = RenderComponent<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='admin-dashboard-metrics']").TextContent.Should().Contain("Active sessions");
            cut.FindAll("button").Should().BeEmpty();
        });
    }
}
