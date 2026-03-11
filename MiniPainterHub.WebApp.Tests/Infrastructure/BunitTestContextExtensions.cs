using System;
using System.Linq;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Tests.Infrastructure;

internal static class BunitTestContextExtensions
{
    private static void EnsureReportStub(this TestContext context)
    {
        if (context.Services.Any(service => service.ServiceType == typeof(IReportService)))
        {
            return;
        }

        context.Services.AddSingleton<IReportService>(new StubReportService());
    }

    public static StubAuthService AddAuthStub(this TestContext context, StubAuthService? stub = null)
    {
        stub ??= new StubAuthService();
        context.Services.AddSingleton<IAuthService>(stub);
        return stub;
    }

    public static StubProfileService AddProfileStub(this TestContext context, StubProfileService? stub = null)
    {
        context.EnsureReportStub();
        stub ??= new StubProfileService();
        context.Services.AddSingleton<IProfileService>(stub);
        return stub;
    }

    public static StubPostService AddPostStub(this TestContext context, StubPostService? stub = null)
    {
        context.EnsureReportStub();
        stub ??= new StubPostService();
        context.Services.AddSingleton<IPostService>(stub);
        return stub;
    }

    public static StubFollowService AddFollowStub(this TestContext context, StubFollowService? stub = null)
    {
        stub ??= new StubFollowService();
        context.Services.AddSingleton<IFollowService>(stub);
        return stub;
    }

    public static StubConversationService AddConversationStub(this TestContext context, StubConversationService? stub = null)
    {
        stub ??= new StubConversationService();
        context.Services.AddSingleton<IConversationService>(stub);
        return stub;
    }

    public static StubCommentService AddCommentStub(this TestContext context, StubCommentService? stub = null)
    {
        context.EnsureReportStub();
        stub ??= new StubCommentService();
        context.Services.AddSingleton<ICommentService>(stub);
        return stub;
    }

    public static StubLikeService AddLikeStub(this TestContext context, StubLikeService? stub = null)
    {
        stub ??= new StubLikeService();
        context.Services.AddSingleton<ILikeService>(stub);
        return stub;
    }

    public static StubModerationService AddModerationStub(this TestContext context, StubModerationService? stub = null)
    {
        context.EnsureReportStub();
        stub ??= new StubModerationService();
        context.Services.AddSingleton<IModerationService>(stub);
        return stub;
    }

    public static StubSearchService AddSearchStub(this TestContext context, StubSearchService? stub = null)
    {
        stub ??= new StubSearchService();
        context.Services.AddSingleton<ISearchService>(stub);
        return stub;
    }

    public static StubReportService AddReportStub(this TestContext context, StubReportService? stub = null)
    {
        stub ??= new StubReportService();
        context.Services.AddSingleton<IReportService>(stub);
        return stub;
    }

    public static string CurrentPath(this TestContext context)
    {
        var navigationManager = context.Services.GetRequiredService<NavigationManager>();
        return new Uri(navigationManager.Uri).AbsolutePath;
    }

    public static void SetAuthenticatedUser(this TestContext context, string userId, string userName)
    {
        var auth = context.AddTestAuthorization();
        auth.SetAuthorized(userName);
        auth.SetClaims(
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, userName));
    }
}
