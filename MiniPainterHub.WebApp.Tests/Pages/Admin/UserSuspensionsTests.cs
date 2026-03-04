using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.WebApp.Services.Http;
using MiniPainterHub.WebApp.Pages.Admin;
using MiniPainterHub.WebApp.Tests.Infrastructure;
using Xunit;

namespace MiniPainterHub.WebApp.Tests.Pages.Admin;

public class UserSuspensionsTests : TestContext
{
    [Fact]
    public void RendersSuspensionAndUnsuspensionInputs()
    {
        this.AddModerationStub();

        var cut = RenderComponent<UserSuspensions>();

        cut.Find("[data-testid='suspend-user-id']").Should().NotBeNull();
        cut.Find("[data-testid='suspend-until']").Should().NotBeNull();
        cut.Find("[data-testid='unsuspend-user-id']").Should().NotBeNull();
        cut.Find("[data-testid='unsuspend-submit']").Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitSuspend_WhenValid_CallsServiceAndShowsSuccessMessage()
    {
        string? capturedUserId = null;
        DateTime? capturedUntilUtc = null;
        string? capturedReason = null;

        this.AddModerationStub(new StubModerationService
        {
            SuspendUserHandler = (userId, request) =>
            {
                capturedUserId = userId;
                capturedUntilUtc = request.SuspendedUntilUtc;
                capturedReason = request.Reason;
                return Task.FromResult(true);
            }
        });

        var cut = RenderComponent<UserSuspensions>();
        cut.Find("[data-testid='suspend-user-id']").Change("target-17");
        cut.Find("[data-testid='suspend-until']").Change("2030-04-10T12:30");
        cut.Find("[data-testid='suspend-reason']").Change("abuse");
        await cut.Find("[data-testid='suspend-submit']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            capturedUserId.Should().Be("target-17");
            capturedUntilUtc.Should().NotBeNull();
            capturedReason.Should().Be("abuse");
            cut.Find("[data-testid='suspend-result']").TextContent.Should().Contain("target-17");
        });
    }

    [Fact]
    public async Task SubmitUnsuspend_WhenUserIdMissing_ShowsValidationAndSkipsServiceCall()
    {
        var called = false;
        this.AddModerationStub(new StubModerationService
        {
            UnsuspendUserHandler = (_, _) =>
            {
                called = true;
                return Task.FromResult(true);
            }
        });

        var cut = RenderComponent<UserSuspensions>();
        await cut.Find("[data-testid='unsuspend-submit']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            called.Should().BeFalse();
            cut.Find("[data-testid='unsuspend-error']").TextContent.Should().Contain("User id is required.");
        });
    }

    [Fact]
    public async Task SearchUsers_WhenResultReturned_SelectingUserPopulatesUserIdFields()
    {
        this.AddModerationStub(new StubModerationService
        {
            SearchUsersHandler = (_, _) => Task.FromResult(
                new ApiResult<IReadOnlyList<ModerationUserLookupDto>?>(true, HttpStatusCode.OK, new[]
                {
                    new ModerationUserLookupDto
                    {
                        UserId = "target-77",
                        UserName = "target",
                        Email = "target@example.test",
                        Roles = new[] { "User" }
                    }
                }))
        });

        var cut = RenderComponent<UserSuspensions>();
        cut.Find("[data-testid='lookup-user-query']").Change("target");
        await cut.Find("[data-testid='lookup-user-submit']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='lookup-user-item']").Should().HaveCount(1);
        });

        await cut.Find("[data-testid='lookup-user-item']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='suspend-user-id']").GetAttribute("value").Should().Be("target-77");
            cut.Find("[data-testid='unsuspend-user-id']").GetAttribute("value").Should().Be("target-77");
        });
    }

    [Fact]
    public async Task SearchUsers_WhenQueryIsEmpty_RequestsSuspendedUsers()
    {
        string? capturedQuery = "sentinel";

        this.AddModerationStub(new StubModerationService
        {
            SearchUsersHandler = (query, _) =>
            {
                capturedQuery = query;
                return Task.FromResult(new ApiResult<IReadOnlyList<ModerationUserLookupDto>?>(true, HttpStatusCode.OK, Array.Empty<ModerationUserLookupDto>()));
            }
        });

        var cut = RenderComponent<UserSuspensions>();
        await cut.Find("[data-testid='lookup-user-submit']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            capturedQuery.Should().BeNull();
            cut.Find("[data-testid='lookup-user-error']").TextContent.Should().Contain("No suspended users");
        });
    }
}
