using System;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Pages;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages;

public class UserProfileTests : TestContext
{
    [Fact]
    public void WhenNoProfileExists_RendersCreateForm()
    {
        this.AddProfileStub(new StubProfileService
        {
            GetMineHandler = () => Task.FromResult<UserProfileDto?>(null)
        });

        var cut = RenderComponent<UserProfile>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='profile-create-display-name']").Should().NotBeNull();
            cut.Find("[data-testid='profile-create-bio']").Should().NotBeNull();
            cut.Find("[data-testid='profile-create-submit']").Should().NotBeNull();
        });
    }

    [Fact]
    public void Create_WhenSubmitSucceeds_ShowsProfilePanel()
    {
        this.AddProfileStub(new StubProfileService
        {
            GetMineHandler = () => Task.FromResult<UserProfileDto?>(null),
            CreateMineHandler = dto => Task.FromResult(new UserProfileDto
            {
                UserId = "user-1",
                DisplayName = dto.DisplayName,
                Bio = dto.Bio
            })
        });

        var cut = RenderComponent<UserProfile>();
        cut.WaitForElement("[data-testid='profile-create-submit']");

        cut.Find("[data-testid='profile-create-display-name']").Change("Painter");
        cut.Find("[data-testid='profile-create-bio']").Change("Mini painter");
        cut.Find("[data-testid='profile-create-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='profile-edit']").Should().NotBeNull();
        });
    }

    [Fact]
    public void WhenProfileExists_RendersProfilePanel()
    {
        this.AddProfileStub(new StubProfileService
        {
            GetMineHandler = () => Task.FromResult<UserProfileDto?>(new UserProfileDto
            {
                UserId = "user-1",
                DisplayName = "Existing",
                Bio = "Existing bio"
            })
        });

        var cut = RenderComponent<UserProfile>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='profile-edit']").Should().NotBeNull();
            cut.Find("[data-testid='profile-display-name']").GetAttribute("value").Should().Be("Existing");
        });
    }

    [Fact]
    public void Save_WhenEditing_UsesProfileServiceAndExitsEditMode()
    {
        UpdateUserProfileDto? captured = null;
        this.AddProfileStub(new StubProfileService
        {
            GetMineHandler = () => Task.FromResult<UserProfileDto?>(new UserProfileDto
            {
                UserId = "user-1",
                DisplayName = "Old Name",
                Bio = "Old Bio"
            }),
            UpdateMineHandler = dto =>
            {
                captured = dto;
                return Task.FromResult(new UserProfileDto
                {
                    UserId = "user-1",
                    DisplayName = dto.DisplayName,
                    Bio = dto.Bio
                });
            }
        });

        var cut = RenderComponent<UserProfile>();
        cut.WaitForElement("[data-testid='profile-edit']");

        cut.Find("[data-testid='profile-edit']").Click();
        cut.Find("[data-testid='profile-display-name']").Change("Updated Name");
        cut.Find("[data-testid='profile-bio']").Change("Updated Bio");
        cut.Find("[data-testid='profile-save']").Click();

        cut.WaitForAssertion(() =>
        {
            captured.Should().NotBeNull();
            captured!.DisplayName.Should().Be("Updated Name");
            captured.Bio.Should().Be("Updated Bio");
            cut.Find("[data-testid='profile-edit']").Should().NotBeNull();
        });
    }

    [Fact]
    public void WhenInitializationFails_FallsBackToCreateForm()
    {
        this.AddProfileStub(new StubProfileService
        {
            GetMineHandler = () => Task.FromException<UserProfileDto?>(new InvalidOperationException("Boom"))
        });

        var cut = RenderComponent<UserProfile>();

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='profile-create-submit']").Should().NotBeNull());
    }
}
