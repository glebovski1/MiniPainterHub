using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class ProfilePanelTests : TestContext
{
    [Fact]
    public void RendersEditButton_WhenOwnerAndNotEditing()
    {
        var cut = RenderComponent<ProfilePanel>(parameters => parameters
            .Add(p => p.Profile, new UserProfileDto { UserId = "user-1", DisplayName = "User One" })
            .Add(p => p.IsOwner, true)
            .Add(p => p.EditEnabled, false));

        cut.Find("[data-testid='profile-edit']").Should().NotBeNull();
    }

    [Fact]
    public void Save_WhenEditing_InvokesOnSaveWithCurrentValues()
    {
        UpdateUserProfileDto? captured = null;
        var cut = RenderComponent<ProfilePanel>(parameters => parameters
            .Add(p => p.Profile, new UserProfileDto { UserId = "user-1", DisplayName = "Old", Bio = "Old bio" })
            .Add(p => p.IsOwner, true)
            .Add(p => p.EditEnabled, true)
            .Add<UpdateUserProfileDto>(p => p.OnSave, dto => captured = dto));

        cut.Find("[data-testid='profile-display-name']").Change("New Name");
        cut.Find("[data-testid='profile-bio']").Change("New bio");
        cut.Find("[data-testid='profile-save']").Click();

        captured.Should().NotBeNull();
        captured!.DisplayName.Should().Be("New Name");
        captured.Bio.Should().Be("New bio");
    }

    [Fact]
    public void RemoveAvatar_WhenNoAvatar_DisablesButton()
    {
        var cut = RenderComponent<ProfilePanel>(parameters => parameters
            .Add(p => p.Profile, new UserProfileDto { UserId = "user-1", DisplayName = "User One", AvatarUrl = null })
            .Add(p => p.IsOwner, true)
            .Add(p => p.EditEnabled, true));

        cut.Find("[data-testid='profile-avatar-remove']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void EditButton_Click_InvokesOnBeginEdit()
    {
        var invoked = false;
        var cut = RenderComponent<ProfilePanel>(parameters => parameters
            .Add(p => p.Profile, new UserProfileDto { UserId = "user-1", DisplayName = "User One" })
            .Add(p => p.IsOwner, true)
            .Add(p => p.EditEnabled, false)
            .Add(p => p.OnBeginEdit, () => invoked = true));

        cut.Find("[data-testid='profile-edit']").Click();

        invoked.Should().BeTrue();
    }
}
