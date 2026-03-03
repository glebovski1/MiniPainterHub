using System;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.WebApp.Services.Interfaces;

namespace MiniPainterHub.WebApp.Tests.Infrastructure;

internal static class BunitTestContextExtensions
{
    public static StubAuthService AddAuthStub(this TestContext context, StubAuthService? stub = null)
    {
        stub ??= new StubAuthService();
        context.Services.AddSingleton<IAuthService>(stub);
        return stub;
    }

    public static StubProfileService AddProfileStub(this TestContext context, StubProfileService? stub = null)
    {
        stub ??= new StubProfileService();
        context.Services.AddSingleton<IProfileService>(stub);
        return stub;
    }

    public static StubPostService AddPostStub(this TestContext context, StubPostService? stub = null)
    {
        stub ??= new StubPostService();
        context.Services.AddSingleton<IPostService>(stub);
        return stub;
    }

    public static StubCommentService AddCommentStub(this TestContext context, StubCommentService? stub = null)
    {
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

    public static string CurrentPath(this TestContext context)
    {
        var navigationManager = context.Services.GetRequiredService<NavigationManager>();
        return new Uri(navigationManager.Uri).AbsolutePath;
    }
}
