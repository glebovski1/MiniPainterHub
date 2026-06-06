using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class AdminControllerTests
{
    [Fact]
    public async Task InboxAndDashboard_WhenModeratorAuthenticated_ReturnOk()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("mod-admin-api", "mod-admin-api", "Moderator");

        var inboxResponse = await client.GetAsync("/api/admin/inbox?pageNumber=1&pageSize=10");
        var dashboardResponse = await client.GetAsync("/api/admin/dashboard?windowHours=2");

        inboxResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inbox = await inboxResponse.Content.ReadFromJsonAsync<PagedResult<AdminInboxItemDto>>();
        inbox.Should().NotBeNull();
        inbox!.PageNumber.Should().Be(1);
        inbox.PageSize.Should().Be(10);

        dashboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<AdminDashboardStatsDto>();
        dashboard.Should().NotBeNull();
        dashboard!.WindowHours.Should().Be(2);
    }

    [Fact]
    public async Task AdminEndpoints_WhenUnauthenticated_ReturnUnauthorized()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var inboxResponse = await client.GetAsync("/api/admin/inbox");
        var controlsResponse = await client.GetAsync("/api/admin/controls");
        var dashboardResponse = await client.GetAsync("/api/admin/dashboard");

        inboxResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        controlsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        dashboardResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Controls_WhenModeratorAuthenticated_ReturnForbidden()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("mod-controls", "mod-controls", "Moderator");

        var getResponse = await client.GetAsync("/api/admin/controls");
        var putResponse = await client.PutAsJsonAsync(
            $"/api/admin/controls/{AdminSiteControlKeys.NewComments}",
            new UpdateAdminSiteControlRequestDto
            {
                Enabled = false,
                Reason = "not allowed"
            });

        getResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        putResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateControl_WhenAdminAuthenticated_PersistsControlAndAudit()
    {
        using var factory = new IntegrationTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        await SeedUserAsync(factory, "admin-controls", "admin-controls");
        using var client = factory.CreateAuthenticatedClient("admin-controls", "admin-controls", "Admin");
        var disabledUntil = DateTime.UtcNow.AddHours(4);

        var response = await client.PutAsJsonAsync(
            $"/api/admin/controls/{AdminSiteControlKeys.NewComments}",
            new UpdateAdminSiteControlRequestDto
            {
                Enabled = false,
                DisabledUntilUtc = disabledUntil,
                Reason = "spam wave",
                Message = "Comments are paused temporarily."
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminSiteControlDto>();
        body.Should().NotBeNull();
        body!.Key.Should().Be(AdminSiteControlKeys.NewComments);
        body.EffectiveEnabled.Should().BeFalse();

        await factory.ExecuteDbContextAsync(async db =>
        {
            var control = await db.AdminSiteControls.SingleAsync(c => c.Key == AdminSiteControlKeys.NewComments);
            control.Enabled.Should().BeFalse();
            control.Reason.Should().Be("spam wave");
            control.Message.Should().Be("Comments are paused temporarily.");
            control.UpdatedByUserId.Should().Be("admin-controls");

            var audit = await db.ModerationAuditLogs.SingleAsync();
            audit.ActionType.Should().Be("SiteControlUpdate");
            audit.TargetType.Should().Be("SiteControl");
            audit.TargetId.Should().Be(AdminSiteControlKeys.NewComments);
            audit.ActorUserId.Should().Be("admin-controls");
        });
    }

    private static Task SeedUserAsync(IntegrationTestApplicationFactory factory, string userId, string userName)
    {
        return factory.ExecuteDbContextAsync(async db =>
        {
            await db.Users.AddAsync(new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                Email = $"{userName}@example.test",
                EmailConfirmed = true
            });
            await db.SaveChangesAsync();
        });
    }
}
