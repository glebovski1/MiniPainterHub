using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.Exceptions;
using MiniPainterHub.Server.Identity;
using MiniPainterHub.Server.Services;
using MiniPainterHub.Server.Services.Interfaces;
using Moq;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class AccountRestrictionServiceTests
{
    [Theory]
    [InlineData(AdminSiteControlKeys.NewPosts, "post")]
    [InlineData(AdminSiteControlKeys.NewComments, "comment")]
    [InlineData(AdminSiteControlKeys.NewRegistrations, "register")]
    public async Task SiteControls_WhenDisabled_BlockMatchingAction(string key, string action)
    {
        var controls = new Mock<IAdminSiteControlService>();
        controls.Setup(s => s.GetControlAsync(key)).ReturnsAsync(new AdminSiteControlDto
        {
            Key = key,
            EffectiveEnabled = false,
            Message = "Paused"
        });
        var service = new AccountRestrictionService(CreateUserManagerMock().Object, controls.Object);

        Func<Task> act = action switch
        {
            "post" => () => service.EnsureCanCreatePostAsync("user-1"),
            "comment" => () => service.EnsureCanCommentAsync("user-1"),
            _ => () => service.EnsureCanRegisterAsync()
        };

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("Paused");
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
