using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Middleware;
using MiniPainterHub.Server.Options;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Tests.Infrastructure;
using Moq;
using Xunit;

namespace MiniPainterHub.Server.Tests.Controllers;

public class MaintenanceModeMiddlewareTests
{
    [Fact]
    public async Task PublicSiteControl_WhenDisabled_EnforcesMaintenanceAndReEnableRestoresAccess()
    {
        using var factory = CreateFactory(enabled: false, allowAdmins: true);
        await factory.ResetDatabaseAsync();
        await factory.ExecuteDbContextAsync(async db =>
        {
            await db.AdminSiteControls.AddAsync(new AdminSiteControl
            {
                Key = AdminSiteControlKeys.PublicSite,
                Enabled = false,
                Message = "Public access is paused.",
                Reason = "incident",
                UpdatedByUserId = "admin-maintenance",
                UpdatedUtc = System.DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
        using var anonymousClient = factory.CreateClient();
        using var adminClient = factory.CreateAuthenticatedClient("admin-maintenance", "admin-maintenance", "Admin");

        var pausedRequest = new HttpRequestMessage(HttpMethod.Get, "/");
        pausedRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        var pausedResponse = await anonymousClient.SendAsync(pausedRequest);

        pausedResponse.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await pausedResponse.Content.ReadAsStringAsync()).Should().Contain("Public access is paused.");

        var bypassResponse = await adminClient.PostAsync("/api/auth/maintenance-bypass", content: null);
        var adminHomeResponse = await adminClient.GetAsync("/");
        bypassResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        adminHomeResponse.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable);

        await factory.ExecuteDbContextAsync(async db =>
        {
            var control = await db.AdminSiteControls.SingleAsync(c => c.Key == AdminSiteControlKeys.PublicSite);
            control.Enabled = true;
            control.DisabledUntilUtc = null;
            await db.SaveChangesAsync();
        });

        var restoredResponse = await anonymousClient.GetAsync("/");
        restoredResponse.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task WhenEnabled_HtmlRequestsReturnMaintenancePage_AndHealthzRemainsAvailable()
    {
        using var factory = CreateFactory(enabled: true, allowAdmins: true);
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var response = await client.SendAsync(request);
        var health = await client.GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().Contain("Maintenance mode");
        health.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WhenEnabled_ApiRequestsReturnProblemDetails()
    {
        using var factory = CreateFactory(enabled: true, allowAdmins: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/posts?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("title").GetString().Should().Be("Maintenance mode");
        json.RootElement.GetProperty("status").GetInt32().Should().Be(503);
    }

    [Fact]
    public async Task AdminBypassCookie_AllowsAuthenticatedAdminThroughMaintenance()
    {
        using var factory = CreateFactory(enabled: true, allowAdmins: true);
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("admin-user", "admin", "Admin");

        var enableResponse = await client.PostAsync("/api/auth/maintenance-bypass", content: null);
        var postsResponse = await client.GetAsync("/api/posts?page=1&pageSize=10");

        enableResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        enableResponse.Headers.Should().Contain(header => header.Key == "Set-Cookie");
        postsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StaticFrameworkAsset_DoesNotQueryDynamicControls()
    {
        var nextCalled = false;
        var middleware = new MaintenanceModeMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new TestOptionsMonitor(new MaintenanceOptions { Enabled = false }),
            Mock.Of<IMaintenanceBypassService>(),
            Mock.Of<IProblemDetailsService>(),
            NullLogger<MaintenanceModeMiddleware>.Instance);
        var controls = new Mock<IAdminSiteControlService>(MockBehavior.Strict);
        var context = new DefaultHttpContext();
        context.Request.Path = "/_framework/Microsoft.AspNetCore.Components.Forms.example.wasm";

        await middleware.InvokeAsync(context, controls.Object);

        nextCalled.Should().BeTrue();
        controls.Verify(s => s.GetControlAsync(It.IsAny<string>()), Times.Never);
    }

    private static IntegrationTestApplicationFactory CreateFactory(bool enabled, bool allowAdmins)
    {
        return new IntegrationTestApplicationFactory(new Dictionary<string, string?>
        {
            ["Maintenance:Enabled"] = enabled.ToString(),
            ["Maintenance:AllowAdmins"] = allowAdmins.ToString(),
            ["Maintenance:Message"] = "Maintenance in progress."
        });
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<MaintenanceOptions>
    {
        public TestOptionsMonitor(MaintenanceOptions currentValue)
        {
            CurrentValue = currentValue;
        }

        public MaintenanceOptions CurrentValue { get; }

        public MaintenanceOptions Get(string? name) => CurrentValue;

        public System.IDisposable? OnChange(System.Action<MaintenanceOptions, string?> listener) => null;
    }
}
