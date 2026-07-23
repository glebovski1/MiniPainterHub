using System;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages.Admin;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class DashboardTests : BunitContext
{
    [Fact]
    public async Task KeepsNewestWindowStatsWhenOlderLoadFinishesLast()
    {
        var delayedSixHourResult = new TaskCompletionSource<ApiResult<AdminDashboardStatsDto?>>();
        this.AddAdminStub(new StubAdminService
        {
            GetDashboardHandler = windowHours => windowHours switch
            {
                6 => delayedSixHourResult.Task,
                2 => Task.FromResult(DashboardResult(windowHours, "Newest 2 hour stats")),
                _ => Task.FromResult(DashboardResult(windowHours, "Initial stats"))
            }
        });

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='admin-dashboard-metrics']").TextContent.Should().Contain("Initial stats"));

        var select = cut.Find("[data-testid='admin-dashboard-window']");
        var sixHourChange = select.TriggerEventAsync("onchange", new ChangeEventArgs { Value = "6" });
        await Task.Delay(25);
        await select.TriggerEventAsync("onchange", new ChangeEventArgs { Value = "2" });

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='admin-dashboard-metrics']").TextContent.Should().Contain("Newest 2 hour stats"));

        delayedSixHourResult.SetResult(DashboardResult(6, "Stale 6 hour stats"));
        await sixHourChange;

        cut.WaitForAssertion(() =>
        {
            var metrics = cut.Find("[data-testid='admin-dashboard-metrics']").TextContent;
            metrics.Should().Contain("Newest 2 hour stats");
            metrics.Should().NotContain("Stale 6 hour stats");
        });
    }

    [Fact]
    public void RerendersWhenPollingLoadsNewStats()
    {
        var callCount = 0;
        this.AddAdminStub(new StubAdminService
        {
            GetDashboardHandler = windowHours =>
            {
                callCount++;
                return Task.FromResult(new ApiResult<AdminDashboardStatsDto?>(true, HttpStatusCode.OK, new AdminDashboardStatsDto
                {
                    GeneratedUtc = DateTime.UtcNow,
                    WindowHours = windowHours,
                    Metrics = new[]
                    {
                        new AdminDashboardMetricDto
                        {
                            Key = "live",
                            Label = "Live counter",
                            Value = callCount == 1 ? "First" : "Second",
                            Status = "Normal"
                        }
                    },
                    Activity = new[]
                    {
                        new AdminDashboardActivityPointDto { TimestampUtc = DateTime.UtcNow, PageViews = callCount, ApiRequests = callCount, ApiErrors = 0 }
                    },
                    Health = new[]
                    {
                        new AdminDashboardHealthDto { Key = "api", Label = "API", Status = "Healthy", Detail = "Healthy" }
                    }
                }));
            }
        });

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='admin-dashboard-metrics']").TextContent.Should().Contain("First"));
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='admin-dashboard-metrics']").TextContent.Should().Contain("Second"),
            TimeSpan.FromSeconds(12));
    }

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

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='admin-dashboard-metrics']").TextContent.Should().Contain("Active sessions");
            cut.Find("[data-testid='admin-activity-chart']").Should().NotBeNull();
            cut.FindAll("[data-testid='admin-activity-empty']").Should().BeEmpty();
            cut.FindAll(".admin-activity-bar").Should().HaveCount(2);
            cut.FindAll("button").Should().BeEmpty();
        });
    }

    [Fact]
    public void RendersInformativeEmptyStateWhenEveryActivityBucketIsZero()
    {
        this.AddAdminStub(new StubAdminService
        {
            GetDashboardHandler = windowHours => Task.FromResult(new ApiResult<AdminDashboardStatsDto?>(true, HttpStatusCode.OK, new AdminDashboardStatsDto
            {
                GeneratedUtc = DateTime.UtcNow,
                WindowHours = windowHours,
                Metrics = Array.Empty<AdminDashboardMetricDto>(),
                Activity = new[]
                {
                    new AdminDashboardActivityPointDto { TimestampUtc = DateTime.UtcNow.AddMinutes(-10) },
                    new AdminDashboardActivityPointDto { TimestampUtc = DateTime.UtcNow }
                },
                Health = Array.Empty<AdminDashboardHealthDto>()
            }))
        });

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            var emptyState = cut.Find("[data-testid='admin-activity-empty']");
            emptyState.GetAttribute("role").Should().Be("status");
            emptyState.TextContent.Should().Contain("No activity recorded");
            emptyState.TextContent.Should().Contain("no page views or API requests");
            cut.FindAll("[data-testid='admin-activity-chart']").Should().BeEmpty();
            cut.FindAll(".admin-activity-bar").Should().BeEmpty();
        });
    }

    private static ApiResult<AdminDashboardStatsDto?> DashboardResult(int windowHours, string value) =>
        new(true, HttpStatusCode.OK, new AdminDashboardStatsDto
        {
            GeneratedUtc = DateTime.UtcNow,
            WindowHours = windowHours,
            Metrics = new[]
            {
                new AdminDashboardMetricDto
                {
                    Key = "window",
                    Label = "Window stats",
                    Value = value,
                    Status = "Normal"
                }
            },
            Activity = new[]
            {
                new AdminDashboardActivityPointDto { TimestampUtc = DateTime.UtcNow, PageViews = windowHours, ApiRequests = windowHours, ApiErrors = 0 }
            },
            Health = new[]
            {
                new AdminDashboardHealthDto { Key = "api", Label = "API", Status = "Healthy", Detail = "Healthy" }
            }
        });
}
