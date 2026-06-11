using System;
using System.Collections.Generic;
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

public class ControlsTests : TestContext
{
    [Fact]
    public void SavingExpiredPauseAsPausedDoesNotReusePastUntil()
    {
        (string Key, UpdateAdminSiteControlRequestDto Request)? saved = null;
        this.AddAdminStub(new StubAdminService
        {
            GetControlsHandler = () => Task.FromResult(new ApiResult<IReadOnlyList<AdminSiteControlDto>?>(true, HttpStatusCode.OK, new[]
            {
                new AdminSiteControlDto
                {
                    Key = AdminSiteControlKeys.PublicSite,
                    Label = "Public site",
                    Description = "Public site",
                    Enabled = false,
                    EffectiveEnabled = true,
                    IsExpired = true,
                    DisabledUntilUtc = DateTime.UtcNow.AddMinutes(-5),
                    Reason = "incident"
                }
            })),
            UpdateControlHandler = (key, request) =>
            {
                saved = (key, request);
                return Task.FromResult(new ApiResult<AdminSiteControlDto?>(true, HttpStatusCode.OK, new AdminSiteControlDto
                {
                    Key = key,
                    Label = "Public site",
                    Description = "Public site",
                    Enabled = request.Enabled,
                    EffectiveEnabled = request.Enabled,
                    DisabledUntilUtc = request.DisabledUntilUtc,
                    Reason = request.Reason
                }));
            }
        });

        var cut = RenderComponent<Controls>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='admin-control-enabled']").HasAttribute("checked").Should().BeTrue());
        cut.Find("[data-testid='admin-control-enabled']").Change(false);
        cut.Find("[data-testid='admin-control-reason']").Change("new incident");
        cut.Find("[data-testid='admin-control-save']").Click();

        cut.WaitForAssertion(() =>
        {
            saved.Should().NotBeNull();
            saved!.Value.Key.Should().Be(AdminSiteControlKeys.PublicSite);
            saved.Value.Request.Enabled.Should().BeFalse();
            saved.Value.Request.DisabledUntilUtc.Should().BeNull();
            saved.Value.Request.Reason.Should().Be("new incident");
        });
    }

    [Fact]
    public void RendersExpiredPauseAsEnabledOnRefresh()
    {
        this.AddAdminStub(new StubAdminService
        {
            GetControlsHandler = () => Task.FromResult(new ApiResult<IReadOnlyList<AdminSiteControlDto>?>(true, HttpStatusCode.OK, new[]
            {
                new AdminSiteControlDto
                {
                    Key = AdminSiteControlKeys.PublicSite,
                    Label = "Public site",
                    Description = "Public site",
                    Enabled = false,
                    EffectiveEnabled = true,
                    IsExpired = true,
                    DisabledUntilUtc = DateTime.UtcNow.AddMinutes(-5),
                    Reason = "incident"
                }
            }))
        });

        var cut = RenderComponent<Controls>();

        cut.WaitForAssertion(() =>
        {
            var row = cut.Find("[data-testid='admin-control-row']");
            row.TextContent.Should().Contain("Expired pause");
            row.TextContent.Should().Contain("Effective enabled");
            row.TextContent.Should().Contain("Enabled");
            cut.Find("[data-testid='admin-control-enabled']").HasAttribute("checked").Should().BeTrue();
            cut.Find("[data-testid='admin-control-until']").HasAttribute("disabled").Should().BeTrue();
        });
    }

    [Fact]
    public void RendersFourControlsAndSavesDraft()
    {
        (string Key, UpdateAdminSiteControlRequestDto Request)? saved = null;
        this.AddAdminStub(new StubAdminService
        {
            GetControlsHandler = () => Task.FromResult(new ApiResult<IReadOnlyList<AdminSiteControlDto>?>(true, HttpStatusCode.OK, new[]
            {
                Control(AdminSiteControlKeys.PublicSite, "Public site"),
                Control(AdminSiteControlKeys.NewPosts, "New posts"),
                Control(AdminSiteControlKeys.NewComments, "New comments"),
                Control(AdminSiteControlKeys.NewRegistrations, "Registrations")
            })),
            UpdateControlHandler = (key, request) =>
            {
                saved = (key, request);
                return Task.FromResult(new ApiResult<AdminSiteControlDto?>(true, HttpStatusCode.OK, new AdminSiteControlDto
                {
                    Key = key,
                    Label = "Updated",
                    Description = "Updated",
                    Enabled = request.Enabled,
                    EffectiveEnabled = request.Enabled,
                    Reason = request.Reason,
                    Message = request.Message
                }));
            }
        });

        var cut = RenderComponent<Controls>();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='admin-control-row']").Should().HaveCount(4));
        cut.FindAll("[data-testid='admin-control-enabled']")[0].Change(false);
        cut.FindAll("[data-testid='admin-control-reason']")[0].Change("spam attack");
        cut.FindAll("[data-testid='admin-control-save']")[0].Click();

        cut.WaitForAssertion(() =>
        {
            saved.Should().NotBeNull();
            saved!.Value.Key.Should().Be(AdminSiteControlKeys.PublicSite);
            saved.Value.Request.Enabled.Should().BeFalse();
            saved.Value.Request.Reason.Should().Be("spam attack");
        });
    }

    private static AdminSiteControlDto Control(string key, string label) => new()
    {
        Key = key,
        Label = label,
        Description = label,
        Enabled = true,
        EffectiveEnabled = true
    };
}
