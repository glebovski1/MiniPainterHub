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
