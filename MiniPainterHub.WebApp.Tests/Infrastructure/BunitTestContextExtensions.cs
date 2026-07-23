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
    private static void EnsureReportStub(this BunitContext context)
    {
        if (context.Services.Any(service => service.ServiceType == typeof(IReportService)))
        {
            return;
        }

        context.Services.AddSingleton<IReportService>(new StubReportService());
    }

    private static void EnsureHobbyProjectStub(this BunitContext context)
    {
        if (context.Services.Any(service => service.ServiceType == typeof(IHobbyProjectService)))
        {
            return;
        }

        context.Services.AddSingleton<IHobbyProjectService>(new StubHobbyProjectService());
    }

    public static StubAuthService AddAuthStub(this BunitContext context, StubAuthService? stub = null)
    {
        stub ??= new StubAuthService();
        context.Services.AddSingleton<IAuthService>(stub);
        return stub;
    }

    public static StubProfileService AddProfileStub(this BunitContext context, StubProfileService? stub = null)
    {
        context.EnsureReportStub();
        context.EnsureHobbyProjectStub();
        stub ??= new StubProfileService();
        context.Services.AddSingleton<IProfileService>(stub);
        return stub;
    }

    public static StubPostService AddPostStub(this BunitContext context, StubPostService? stub = null)
    {
        context.EnsureReportStub();
        context.EnsureHobbyProjectStub();
        context.AddPostViewerStub();
        context.AddAuthorMarkStub();
        context.AddCommentMarkStub();
        stub ??= new StubPostService();
        context.Services.AddSingleton<IPostService>(stub);
        return stub;
    }

    public static StubPostViewerService AddPostViewerStub(this BunitContext context, StubPostViewerService? stub = null)
    {
        stub ??= new StubPostViewerService();
        context.Services.AddSingleton<IPostViewerService>(stub);
        return stub;
    }

    public static StubPaintingGuideService AddGuideStub(this BunitContext context, StubPaintingGuideService? stub = null)
    {
        stub ??= new StubPaintingGuideService();
        context.Services.AddSingleton<IPaintingGuideService>(stub);
        return stub;
    }

    public static StubNewsAnnouncementService AddNewsStub(this BunitContext context, StubNewsAnnouncementService? stub = null)
    {
        stub ??= new StubNewsAnnouncementService();
        context.Services.AddSingleton<INewsAnnouncementService>(stub);
        return stub;
    }

    public static StubAuthorMarkService AddAuthorMarkStub(this BunitContext context, StubAuthorMarkService? stub = null)
    {
        stub ??= new StubAuthorMarkService();
        context.Services.AddSingleton<IAuthorMarkService>(stub);
        return stub;
    }

    public static StubCommentMarkService AddCommentMarkStub(this BunitContext context, StubCommentMarkService? stub = null)
    {
        stub ??= new StubCommentMarkService();
        context.Services.AddSingleton<ICommentMarkService>(stub);
        return stub;
    }

    public static StubFollowService AddFollowStub(this BunitContext context, StubFollowService? stub = null)
    {
        stub ??= new StubFollowService();
        context.Services.AddSingleton<IFollowService>(stub);
        return stub;
    }

    public static StubConversationService AddConversationStub(this BunitContext context, StubConversationService? stub = null)
    {
        if (!context.Services.Any(service => service.ServiceType == typeof(ISupportTicketService)))
        {
            context.AddSupportStub();
        }

        stub ??= new StubConversationService();
        context.Services.AddSingleton<IConversationService>(stub);
        context.Services.AddSingleton<IConversationSummaryService>(stub);
        return stub;
    }

    public static StubSupportTicketService AddSupportStub(this BunitContext context, StubSupportTicketService? stub = null)
    {
        stub ??= new StubSupportTicketService();
        context.Services.AddSingleton<ISupportTicketService>(stub);
        return stub;
    }

    public static StubCommentService AddCommentStub(this BunitContext context, StubCommentService? stub = null)
    {
        context.EnsureReportStub();
        context.AddCommentMarkStub();
        context.AddAuthorMarkStub();
        stub ??= new StubCommentService();
        context.Services.AddSingleton<ICommentService>(stub);
        return stub;
    }

    public static StubLikeService AddLikeStub(this BunitContext context, StubLikeService? stub = null)
    {
        stub ??= new StubLikeService();
        context.Services.AddSingleton<ILikeService>(stub);
        return stub;
    }

    public static StubModerationService AddModerationStub(this BunitContext context, StubModerationService? stub = null)
    {
        context.EnsureReportStub();
        stub ??= new StubModerationService();
        context.Services.AddSingleton<IModerationService>(stub);
        return stub;
    }

    public static StubSearchService AddSearchStub(this BunitContext context, StubSearchService? stub = null)
    {
        stub ??= new StubSearchService();
        context.Services.AddSingleton<ISearchService>(stub);
        return stub;
    }

    public static StubHobbyProjectService AddHobbyProjectStub(this BunitContext context, StubHobbyProjectService? stub = null)
    {
        stub ??= new StubHobbyProjectService();
        context.Services.AddSingleton<IHobbyProjectService>(stub);
        return stub;
    }

    public static StubReportService AddReportStub(this BunitContext context, StubReportService? stub = null)
    {
        stub ??= new StubReportService();
        context.Services.AddSingleton<IReportService>(stub);
        return stub;
    }

    public static StubAdminService AddAdminStub(this BunitContext context, StubAdminService? stub = null)
    {
        stub ??= new StubAdminService();
        context.Services.AddSingleton<IAdminService>(stub);
        return stub;
    }

    public static string CurrentPath(this BunitContext context)
    {
        var navigationManager = context.Services.GetRequiredService<NavigationManager>();
        return new Uri(navigationManager.Uri).AbsolutePath;
    }

    public static void SetAuthenticatedUser(this BunitContext context, string userId, string userName)
    {
        var auth = context.AddAuthorization();
        auth.SetAuthorized(userName);
        auth.SetClaims(
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, userName));
    }
}
