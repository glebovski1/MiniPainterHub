using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Entities;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Tests.Infrastructure;
using Moq;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class AdminSiteControlServiceTests
{
    [Fact]
    public async Task GetControlAsync_WhenDisabledUntilExpired_ComputesEffectiveEnabled()
    {
        await using var context = AppDbContextFactory.Create();
        await context.AdminSiteControls.AddAsync(new AdminSiteControl
        {
            Key = AdminSiteControlKeys.NewComments,
            Enabled = false,
            DisabledUntilUtc = DateTime.UtcNow.AddMinutes(-5),
            Reason = "incident",
            UpdatedByUserId = "admin-1",
            UpdatedUtc = DateTime.UtcNow.AddHours(-1)
        });
        await context.SaveChangesAsync();

        var service = new AdminSiteControlService(context, CreateUserManagerMock().Object);

        var control = await service.GetControlAsync(AdminSiteControlKeys.NewComments);

        control.Enabled.Should().BeFalse();
        control.EffectiveEnabled.Should().BeTrue();
        control.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateControlAsync_WhenDisabled_WritesAuditRow()
    {
        await using var context = AppDbContextFactory.Create();
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.FindByIdAsync("admin-1")).ReturnsAsync(TestData.CreateUser("admin-1", "admin"));
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(["Admin"]);
        var service = new AdminSiteControlService(context, userManager.Object);

        var control = await service.UpdateControlAsync(AdminSiteControlKeys.NewPosts, new UpdateAdminSiteControlRequestDto
        {
            Enabled = false,
            DisabledUntilUtc = DateTime.UtcNow.AddHours(2),
            Message = "Uploads paused",
            Reason = "spam wave"
        }, "admin-1");

        control.EffectiveEnabled.Should().BeFalse();
        control.Message.Should().Be("Uploads paused");

        var audit = await context.ModerationAuditLogs.SingleAsync();
        audit.ActionType.Should().Be("SiteControlUpdate");
        audit.TargetType.Should().Be("SiteControl");
        audit.TargetId.Should().Be(AdminSiteControlKeys.NewPosts);
        audit.Reason.Should().Be("spam wave");
    }

    [Fact]
    public async Task UpdateControlAsync_WhenReEnabledAfterPause_PersistsNormalState()
    {
        await using var context = AppDbContextFactory.Create();
        await context.AdminSiteControls.AddAsync(new AdminSiteControl
        {
            Key = AdminSiteControlKeys.PublicSite,
            Enabled = false,
            DisabledUntilUtc = DateTime.UtcNow.AddHours(1),
            Message = "Site paused",
            Reason = "incident",
            UpdatedByUserId = "admin-1",
            UpdatedUtc = DateTime.UtcNow.AddMinutes(-30)
        });
        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.FindByIdAsync("admin-1")).ReturnsAsync(TestData.CreateUser("admin-1", "admin"));
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(["Admin"]);
        var service = new AdminSiteControlService(context, userManager.Object);

        var control = await service.UpdateControlAsync(AdminSiteControlKeys.PublicSite, new UpdateAdminSiteControlRequestDto
        {
            Enabled = true,
            Message = " ",
            Reason = " "
        }, "admin-1");

        control.Enabled.Should().BeTrue();
        control.EffectiveEnabled.Should().BeTrue();
        control.DisabledUntilUtc.Should().BeNull();
        control.Message.Should().BeNull();
        control.Reason.Should().BeNull();

        var reloaded = await service.GetControlAsync(AdminSiteControlKeys.PublicSite);
        reloaded.Enabled.Should().BeTrue();
        reloaded.EffectiveEnabled.Should().BeTrue();
        reloaded.DisabledUntilUtc.Should().BeNull();
        reloaded.Message.Should().BeNull();
        reloaded.Reason.Should().BeNull();
    }

    [Fact]
    public async Task UpdateControlAsync_WhenPausingWithoutReason_RejectsUpdate()
    {
        await using var context = AppDbContextFactory.Create();
        var service = new AdminSiteControlService(context, CreateUserManagerMock().Object);

        Func<Task> act = () => service.UpdateControlAsync(AdminSiteControlKeys.NewRegistrations, new UpdateAdminSiteControlRequestDto
        {
            Enabled = false
        }, "admin-1");

        await act.Should().ThrowAsync<DomainValidationException>()
            .Where(ex => ex.Errors.ContainsKey("reason"));
        context.AdminSiteControls.Should().BeEmpty();
        context.ModerationAuditLogs.Should().BeEmpty();
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }
}
