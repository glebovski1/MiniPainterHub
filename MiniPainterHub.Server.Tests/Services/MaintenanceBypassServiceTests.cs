using System;
using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using MiniPainterHub.Server.Services;
using Xunit;

namespace MiniPainterHub.Server.Tests.Services;

public class MaintenanceBypassServiceTests
{
    [Fact]
    public void AppendCookie_ThenTryValidate_RoundTripsUserId()
    {
        var provider = CreateProvider();
        var service = new MaintenanceBypassService(provider);
        var responseContext = new DefaultHttpContext();
        responseContext.Request.Scheme = "https";

        service.AppendCookie(responseContext.Response, "user-1");

        var setCookie = responseContext.Response.Headers.SetCookie.ToString();
        var token = ExtractCookieValue(setCookie, service.CookieName);
        var requestContext = new DefaultHttpContext();
        requestContext.Request.Headers.Cookie = $"{service.CookieName}={token}";

        var valid = service.TryValidate(requestContext.Request, out var userId);

        valid.Should().BeTrue();
        userId.Should().Be("user-1");
        setCookie.ToLowerInvariant().Should().Contain("secure");
    }

    [Fact]
    public void ClearCookie_WritesCookieDeletionHeader()
    {
        var provider = CreateProvider();
        var service = new MaintenanceBypassService(provider);
        var context = new DefaultHttpContext();

        service.ClearCookie(context.Response);

        context.Response.Headers.SetCookie.ToString().Should().Contain(service.CookieName);
    }

    private static IDataProtectionProvider CreateProvider()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "mph-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempPath);
        return DataProtectionProvider.Create(new DirectoryInfo(tempPath));
    }

    private static string ExtractCookieValue(string setCookieHeader, string cookieName)
    {
        var prefix = cookieName + "=";
        var start = setCookieHeader.IndexOf(prefix, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        var valueStart = start + prefix.Length;
        var end = setCookieHeader.IndexOf(';', valueStart);
        return end >= 0
            ? setCookieHeader[valueStart..end]
            : setCookieHeader[valueStart..];
    }
}
